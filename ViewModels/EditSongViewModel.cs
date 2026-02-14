using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Telhai.DotNet.PlayerProject.Models;
using Telhai.DotNet.PlayerProject.MVVM;
using Telhai.DotNet.PlayerProject.Services;

namespace Telhai.DotNet.PlayerProject.ViewModels
{
    public class EditSongViewModel : ObservableObject
    {
        private readonly MetadataCacheService _cache;
        private readonly SongMetadata _meta;

        private string _displayTitle;
        public string DisplayTitle
        {
            get => _displayTitle;
            set => SetProperty(ref _displayTitle, value);
        }

        public string FilePath => _meta.TrackFilePath;

        public string? Artist => _meta.ApiArtistName;
        public string? Album => _meta.ApiAlbumName;

        private BitmapImage? _coverImage;
        public BitmapImage? CoverImage
        {
            get => _coverImage;
            set => SetProperty(ref _coverImage, value);
        }

        public ObservableCollection<string> Images { get; }

        public RelayCommand AddImageCommand { get; }
        public RelayCommand RemoveSelectedImageCommand { get; }
        public RelayCommand SaveCommand { get; }

        private string? _selectedImage;
        public string? SelectedImage
        {
            get => _selectedImage;
            set
            {
                if (SetProperty(ref _selectedImage, value))
                    RemoveSelectedImageCommand.RaiseCanExecuteChanged();
            }
        }

        public event Action? RequestClose;

        public EditSongViewModel(MetadataCacheService cache, SongMetadata meta)
        {
            _cache = cache;
            _meta = meta;

            _displayTitle = meta.DisplayTitle;
            Images = new ObservableCollection<string>(meta.ExtraImagePaths ?? Enumerable.Empty<string>());

            CoverImage = LoadImageOrNull(meta.ApiCoverLocalPath);

            AddImageCommand = new RelayCommand(AddImage);
            RemoveSelectedImageCommand = new RelayCommand(RemoveSelectedImage, () => !string.IsNullOrWhiteSpace(SelectedImage));
            SaveCommand = new RelayCommand(Save);
        }

        private void AddImage()
        {
            var ofd = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
            };

            if (ofd.ShowDialog() != true) return;

            var songDir = _cache.EnsureSongFolder(_meta.TrackFilePath);
            var imagesDir = Path.Combine(songDir, "Images");

            foreach (var src in ofd.FileNames)
            {
                try
                {
                    var dest = Path.Combine(imagesDir, Path.GetFileName(src));
                    dest = EnsureUniquePath(dest);

                    File.Copy(src, dest, overwrite: false);

                    Images.Add(dest);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to add image:\n{src}\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemoveSelectedImage()
        {
            if (string.IsNullOrWhiteSpace(SelectedImage)) return;

            var toRemove = SelectedImage;

            // Remove from collection
            Images.Remove(toRemove);
            SelectedImage = null;

            // Optional: also delete from disk if it's under our SongData folder
            try
            {
                if (toRemove.StartsWith(Path.GetFullPath("SongData"), StringComparison.OrdinalIgnoreCase) && File.Exists(toRemove))
                    File.Delete(toRemove);
            }
            catch { /* ignore */ }
        }

        private void Save()
        {
            _meta.DisplayTitle = DisplayTitle?.Trim() ?? "";
            _meta.ExtraImagePaths = Images.ToList();

            _cache.Upsert(_meta);

            RequestClose?.Invoke();
        }

        private static BitmapImage? LoadImageOrNull(string? path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return null;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(Path.GetFullPath(path));
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private static string EnsureUniquePath(string path)
        {
            if (!File.Exists(path)) return path;

            var dir = Path.GetDirectoryName(path)!;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);

            for (int i = 1; i < 10_000; i++)
            {
                var candidate = Path.Combine(dir, $"{name}_{i}{ext}");
                if (!File.Exists(candidate)) return candidate;
            }

            // fallback
            return Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
        }
    }
}
