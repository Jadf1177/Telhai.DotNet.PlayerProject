using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Telhai.DotNet.PlayerProject.Models;

namespace Telhai.DotNet.PlayerProject.Services
{
    public class MetadataCacheService
    {
        private const string CACHE_FILE = "metadata-cache.json";
        private readonly JsonSerializerOptions _options = new JsonSerializerOptions { WriteIndented = true };

        private readonly Dictionary<string, SongMetadata> _items;

        public MetadataCacheService()
        {
            _items = LoadAll();
        }

        public SongMetadata GetOrCreate(string trackFilePath, string defaultTitle)
        {
            if (!_items.TryGetValue(trackFilePath, out var meta))
            {
                meta = new SongMetadata
                {
                    TrackFilePath = trackFilePath,
                    DisplayTitle = defaultTitle,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _items[trackFilePath] = meta;
                SaveAll();
            }

            // If title was never set, initialize it.
            if (string.IsNullOrWhiteSpace(meta.DisplayTitle))
                meta.DisplayTitle = defaultTitle;

            return meta;
        }

        public bool TryGet(string trackFilePath, out SongMetadata meta)
            => _items.TryGetValue(trackFilePath, out meta!);

        public void Upsert(SongMetadata meta)
        {
            meta.UpdatedAtUtc = DateTime.UtcNow;
            _items[meta.TrackFilePath] = meta;
            SaveAll();
        }

        private Dictionary<string, SongMetadata> LoadAll()
        {
            try
            {
                if (!File.Exists(CACHE_FILE))
                    return new Dictionary<string, SongMetadata>();

                var json = File.ReadAllText(CACHE_FILE);
                var list = JsonSerializer.Deserialize<List<SongMetadata>>(json) ?? new List<SongMetadata>();
                return list
                    .Where(x => !string.IsNullOrWhiteSpace(x.TrackFilePath))
                    .GroupBy(x => x.TrackFilePath)
                    .ToDictionary(g => g.Key, g => g.Last());
            }
            catch
            {
                // Corrupt cache -> start fresh
                return new Dictionary<string, SongMetadata>();
            }
        }

        private void SaveAll()
        {
            var list = _items.Values.OrderBy(x => x.TrackFilePath).ToList();
            var json = JsonSerializer.Serialize(list, _options);
            File.WriteAllText(CACHE_FILE, json);
        }

        public string EnsureSongFolder(string trackFilePath)
        {
            var safe = MakeSafeFolderName(trackFilePath);
            var dir = Path.Combine("SongData", safe);
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(Path.Combine(dir, "Images"));
            Directory.CreateDirectory(Path.Combine(dir, "Covers"));
            return dir;
        }

        private static string MakeSafeFolderName(string input)
        {
            // Use a stable hash so paths are valid and unique
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash).Substring(0, 16);
        }
    }
}
