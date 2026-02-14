using Microsoft.Win32;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Telhai.DotNet.PlayerProject.Models;
using Telhai.DotNet.PlayerProject.Services;
using Telhai.DotNet.PlayerProject.ViewModels;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Telhai.DotNet.PlayerProject
{
    /// <summary>
    /// Interaction logic for MusicPlayer.xaml
    /// </summary>
    public partial class MusicPlayer : Window
    {
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private DispatcherTimer timer = new DispatcherTimer();
        private List<MusicTrack> library = new List<MusicTrack>();
        private bool isDragging = false;
        private const string FILE_NAME = "library.json";

        // --- Services ---
        private readonly ItunesSearchService _itunes = new ItunesSearchService();
        private readonly MetadataCacheService _cache = new MetadataCacheService();

        // Cancel previous API request when switching songs
        private CancellationTokenSource? _apiCts;

        // Slideshow for per-song images
        private DispatcherTimer _slideshowTimer = new DispatcherTimer();
        private int _slideshowIndex = 0;
        private List<string> _currentImageLoop = new List<string>();


        public MusicPlayer()
        {
            //--init all Hardcoded xaml into Elements Tree
            InitializeComponent();

            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += new EventHandler(Timer_Tick);

            _slideshowTimer.Interval = TimeSpan.FromSeconds(3);
            _slideshowTimer.Tick += SlideshowTimer_Tick;

            this.Loaded += MusicPlayer_Loaded;
            // this.MouseDoubleClick += MusicPlayer_MouseDoubleClick;
            // this.MouseDoubleClick += new MouseButtonEventHandler(MusicPlayer_MouseDoubleClick);
        }

        private void MusicPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            this.LoadLibrary();
        }


        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Update slider ONLY if music is loaded AND user is NOT holding the handle
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan && !isDragging)
            {
                sliderProgress.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                sliderProgress.Value = mediaPlayer.Position.TotalSeconds;
            }
        }

        // --- EMPTY PLACEHOLDERS TO MAKE IT BUILD ---
        private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            // If user selected a track, (re)load it and play.
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                await StartPlayingTrackAsync(track);
                return;
            }

            // Otherwise, just resume current media (if already loaded).
            mediaPlayer.Play();
            timer.Start();
            txtStatus.Text = "Playing";
        }
        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
            txtStatus.Text = "Paused";
        }
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Stop();
            timer.Stop();
            sliderProgress.Value = 0;
            txtStatus.Text = "Stopped";
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaPlayer.Volume = sliderVolume.Value;
        }

        private void Slider_DragStarted(object sender, MouseButtonEventArgs e)
        {
            isDragging = true; // Stop timer updates
        }

        private void Slider_DragCompleted(object sender, MouseButtonEventArgs e)
        {
            isDragging = false; // Resume timer updates
            mediaPlayer.Position = TimeSpan.FromSeconds(sliderProgress.Value);
        }


        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            //File Dialog to choose file from system
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Filter = "MP3 Files|*.mp3";

            //User Confirmed
            if (ofd.ShowDialog() == true)
            {
                //iterate all files selected as tring
                foreach (string file in ofd.FileNames)
                {
                    //Create Object for each filr
                    MusicTrack track = new MusicTrack
                    {
                        //Only file name
                        Title = System.IO.Path.GetFileNameWithoutExtension(file),
                        //full path
                        FilePath = file
                    };
                    library.Add(track);
                }
                UpdateLibraryUI();
                SaveLibrary();
            }
        }

        private void UpdateLibraryUI()
        {
            //Take All library list as Source to the listbox
            //diaplay tostring for inner object whithin list
            lstLibrary.ItemsSource = null;
            lstLibrary.ItemsSource = library;
        }

        private void SaveLibrary()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(library, options);
            File.WriteAllText(FILE_NAME, json);
        }

        private void LoadLibrary()
        {
            if (File.Exists(FILE_NAME))
            {
                //read File
                string json = File.ReadAllText(FILE_NAME);
                //Create List Of MusicTrack from json
                library = JsonSerializer.Deserialize<List<MusicTrack>>(json) ?? new List<MusicTrack>();
                //Show All loaded MusicTrack in List Box
                UpdateLibraryUI();
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                library.Remove(track);
                UpdateLibraryUI();
                SaveLibrary();
            }
        }

        private void LstLibrary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstLibrary.SelectedItem is not MusicTrack track) return;

            // Single click: show file title + full path (not from API)
            txtCurrentSong.Text = track.Title;
            txtFilePath.Text = track.FilePath;

            // Show cached data if exists (no API call)
            if (_cache.TryGet(track.FilePath, out var meta))
            {
                ApplyMetadataToUi(meta);
            }
            else
            {
                // Reset UI to defaults
                ApplyMetadataToUi(_cache.GetOrCreate(track.FilePath, track.Title));
            }
        }

        private void BtnEditSong_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is not MusicTrack track)
            {
                MessageBox.Show("Select a song first.", "Edit Song", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var meta = _cache.GetOrCreate(track.FilePath, track.Title);

            var vm = new EditSongViewModel(_cache, meta);
            var win = new EditSongWindow(vm)
            {
                Owner = this
            };

            win.ShowDialog();

            // Refresh UI after save
            if (_cache.TryGet(track.FilePath, out var updated))
                ApplyMetadataToUi(updated);
        }


        private async void LstLibrary_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
                await StartPlayingTrackAsync(track);
        }

        
        private async Task StartPlayingTrackAsync(MusicTrack track)
        {
            // Start audio immediately (no UI blocking)
            mediaPlayer.Open(new Uri(track.FilePath));
            mediaPlayer.Play();
            timer.Start();
            txtStatus.Text = "Playing";

            // Update basic UI now
            txtCurrentSong.Text = track.Title;
            txtFilePath.Text = track.FilePath;

            // Cancel previous API request (avoid unnecessary calls)
            _apiCts?.Cancel();
            _apiCts?.Dispose();
            _apiCts = new CancellationTokenSource();

            // 1) If metadata already exists in cache -> show it without API call
            if (_cache.TryGet(track.FilePath, out var cached))
            {
                ApplyMetadataToUi(cached);
                return;
            }

            // 2) Otherwise, call API asynchronously in parallel to playback
            var meta = _cache.GetOrCreate(track.FilePath, track.Title);

            try
            {
                var term = BuildSearchTermFromFileName(track.Title);
                var best = await _itunes.SearchBestMatchAsync(term, _apiCts.Token);

                if (best != null)
                {
                    meta.ApiTrackName = best.TrackName;
                    meta.ApiArtistName = best.ArtistName;
                    meta.ApiAlbumName = best.CollectionName;

                    // Cover: download once and store locally so we won't need API again
                    var artworkUrl = best.ArtworkUrl600 ?? best.ArtworkUrl100;

                    if (!string.IsNullOrWhiteSpace(artworkUrl))
                    {
                        var songDir = _cache.EnsureSongFolder(track.FilePath);
                        var coversDir = System.IO.Path.Combine(songDir, "Covers");

                        var bytes = await _itunes.DownloadBytesAsync(artworkUrl, _apiCts.Token);
                        if (bytes != null && bytes.Length > 0)
                        {
                            var fileName = "api-cover.jpg";
                            var localCover = System.IO.Path.Combine(coversDir, fileName);
                            await File.WriteAllBytesAsync(localCover, bytes, _apiCts.Token);
                            meta.ApiCoverLocalPath = localCover;
                        }
                    }

                    _cache.Upsert(meta);
                    ApplyMetadataToUi(meta);
                }
                else
                {
                    // No match -> still show filename/path (requirement when error/no data)
                    ApplyMetadataToUi(meta);
                }
            }
            catch (OperationCanceledException)
            {
                // user switched songs quickly -> ignore
            }
            catch
            {
                // Requirement: in error, show file name (without extension) + full path.
                // We already show those in txtCurrentSong/txtFilePath.
                ApplyMetadataToUi(meta);
            }
        }

        private static string BuildSearchTermFromFileName(string fileNameNoExt)
        {
            // Requirement: "artist - song" or "artist song" etc.
            // Convert common separators to spaces, then trim.
            var term = fileNameNoExt.Replace("-", " ").Replace("_", " ");
            term = string.Join(" ", term.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return term.Trim();
        }

        private void ApplyMetadataToUi(SongMetadata meta)
        {
            // Choose displayed title:
            txtApiSong.Text = string.IsNullOrWhiteSpace(meta.ApiTrackName) ? meta.DisplayTitle : meta.ApiTrackName!;
            txtArtist.Text = string.IsNullOrWhiteSpace(meta.ApiArtistName) ? "-" : meta.ApiArtistName!;
            txtAlbum.Text = string.IsNullOrWhiteSpace(meta.ApiAlbumName) ? "-" : meta.ApiAlbumName!;

            // Cover + slideshow setup
            var loop = new List<string>();

            if (meta.ExtraImagePaths != null && meta.ExtraImagePaths.Count > 0)
                loop.AddRange(meta.ExtraImagePaths.Where(File.Exists));

            if (loop.Count == 0 && !string.IsNullOrWhiteSpace(meta.ApiCoverLocalPath) && File.Exists(meta.ApiCoverLocalPath))
                loop.Add(meta.ApiCoverLocalPath);

            if (loop.Count == 0)
                loop.Add(GetDefaultCoverPath());

            _currentImageLoop = loop;
            _slideshowIndex = 0;

            SetCoverImage(_currentImageLoop[0]);

            if (_currentImageLoop.Count > 1)
                _slideshowTimer.Start();
            else
                _slideshowTimer.Stop();
        }

        private void SlideshowTimer_Tick(object? sender, EventArgs e)
        {
            if (_currentImageLoop == null || _currentImageLoop.Count == 0) return;

            _slideshowIndex++;
            if (_slideshowIndex >= _currentImageLoop.Count)
                _slideshowIndex = 0;

            SetCoverImage(_currentImageLoop[_slideshowIndex]);
        }

        private void SetCoverImage(string path)
        {
            try
            {
                if (!File.Exists(path)) return;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(Path.GetFullPath(path));
                bmp.EndInit();
                bmp.Freeze();

                imgCover.Source = bmp;
            }
            catch
            {
                // ignore
            }
        }

        private static string GetDefaultCoverPath()
        {
            // Put a simple default image file under project output folder.
            // If missing, we just won't change the image.
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "default-cover.png");
            return path;
        }

private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            //1) Create Settings Window Instance
            Settings settingsWin = new Settings();

            //2) Subscribe/register to OnScanCompleted Event
            settingsWin.OnScanCompleted += SettingsWin_OnScanCompleted;

            settingsWin.ShowDialog();

        }

        private void SettingsWin_OnScanCompleted(List<MusicTrack> newTracksEventData)
        {
            foreach (var track in newTracksEventData)
            {
                // Prevent duplicates based on FilePath
                if (!library.Any(x => x.FilePath == track.FilePath))
                {
                    library.Add(track);
                }
            }

            UpdateLibraryUI();
            SaveLibrary();
        }
    }



    //private void MusicPlayer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    //{
    //    MainWindow p = new MainWindow();
    //    p.Title = "YYYYY";
    //    p.Show();
    //}
}