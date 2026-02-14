using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Telhai.DotNet.PlayerProject.Models
{
    public class SongMetadata
    {
        // Key we use for caching (full local path is safest and unique).
        public string TrackFilePath { get; set; } = string.Empty;

        // Display title (can be edited by user)
        public string DisplayTitle { get; set; } = string.Empty;

        // iTunes API metadata (optional)
        public string? ApiTrackName { get; set; }
        public string? ApiArtistName { get; set; }
        public string? ApiAlbumName { get; set; }

        // Local cached cover image path (downloaded once)
        public string? ApiCoverLocalPath { get; set; }

        // Extra per-song images added in the edit window (local file paths)
        public List<string> ExtraImagePaths { get; set; } = new List<string>();

        // When this cache item was last updated (for debugging)
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
