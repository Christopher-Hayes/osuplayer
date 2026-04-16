using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Reactive.Disposables;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using DynamicData;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Data.DataModels;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.Data.OsuPlayer.Classes;
using OsuPlayer.Data.OsuPlayer.StorageModels;
using OsuPlayer.Interfaces.Service;
using OsuPlayer.IO.Storage.Playlists;
using OsuPlayer.Modules.Audio.Interfaces;
using OsuPlayer.Network.LastFm.Responses;
using OsuPlayer.Network.MusicBrainz;
using ReactiveUI;
using Splat;

namespace OsuPlayer.Views;

public class ArtistViewModel : BaseViewModel
{
    public readonly IPlayer Player;

    private string _artistName = string.Empty;
    private ReadOnlyObservableCollection<IMapEntryBase>? _songs;
    private string? _biography;
    private string? _listeners;
    private string? _playCount;
    private List<string>? _tags;
    private List<SimilarArtistDisplayEntry>? _similarArtists;
    private bool _isLastFmAvailable;
    private bool _isLoading;
    private bool _hasLastFmData;
    private Bitmap? _artistImage;
    private List<Playlist>? _playlists;
    private List<AddToPlaylistContextMenuEntry>? _playlistContextMenuEntries;
    private bool _isAddToPlaylistPopupOpen;

    public string ArtistName
    {
        get => _artistName;
        set => this.RaiseAndSetIfChanged(ref _artistName, value);
    }

    public ReadOnlyObservableCollection<IMapEntryBase>? Songs => _songs;

    public string? Biography
    {
        get => _biography;
        set => this.RaiseAndSetIfChanged(ref _biography, value);
    }

    public string? Listeners
    {
        get => _listeners;
        set => this.RaiseAndSetIfChanged(ref _listeners, value);
    }

    public string? PlayCount
    {
        get => _playCount;
        set => this.RaiseAndSetIfChanged(ref _playCount, value);
    }

    public List<string>? Tags
    {
        get => _tags;
        set => this.RaiseAndSetIfChanged(ref _tags, value);
    }

    public List<SimilarArtistDisplayEntry>? SimilarArtists
    {
        get => _similarArtists;
        set => this.RaiseAndSetIfChanged(ref _similarArtists, value);
    }

    public bool IsLastFmAvailable
    {
        get => _isLastFmAvailable;
        set => this.RaiseAndSetIfChanged(ref _isLastFmAvailable, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool HasLastFmData
    {
        get => _hasLastFmData;
        set => this.RaiseAndSetIfChanged(ref _hasLastFmData, value);
    }

    /// <summary>
    /// Artist photo loaded from the local cache (downloaded via MusicBrainz → Wikidata → Wikimedia Commons).
    /// Null when no image is available or not yet loaded.
    /// </summary>
    public Bitmap? ArtistImage
    {
        get => _artistImage;
        set => this.RaiseAndSetIfChanged(ref _artistImage, value);
    }

    public bool DisplaySongListCovers => new Config().Container.DisplaySongListCovers;

    public List<AddToPlaylistContextMenuEntry>? PlaylistContextMenuEntries
    {
        get => _playlistContextMenuEntries;
        set => this.RaiseAndSetIfChanged(ref _playlistContextMenuEntries, value);
    }

    public bool IsAddToPlaylistPopupOpen
    {
        get => _isAddToPlaylistPopupOpen;
        set => this.RaiseAndSetIfChanged(ref _isAddToPlaylistPopupOpen, value);
    }

    public ArtistViewModel(IPlayer player)
    {
        Player = player;

        Activator = new ViewModelActivator();

        this.WhenActivated(disposables =>
        {
            Disposable.Create(() => { }).DisposeWith(disposables);
        });
    }

    // Parameterless constructor for the designer
    public ArtistViewModel() : this(Locator.Current.GetRequiredService<IPlayer>())
    {
    }

    /// <summary>
    /// Loads the artist page for the given artist name
    /// </summary>
    public async Task LoadArtistAsync(string artistName)
    {
        ArtistName = artistName;
        ArtistImage = null;

        // Load playlists for the "Add to playlist" button
        _playlists = (await PlaylistManager.GetAllPlaylistsAsync())?.ToList();
        PlaylistContextMenuEntries = _playlists?.Select(x => new AddToPlaylistContextMenuEntry(x.Name, AddAllSongsToPlaylist)).ToList();

        // Populate songs by this artist
        var allSongs = Player.SongSourceProvider.SongSourceList;
        IMapEntryBase? firstSong = null;
        if (allSongs != null)
        {
            var artistSongs = allSongs
                .Where(s => string.Equals(s.Artist, artistName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            firstSong = artistSongs.FirstOrDefault();

            _songs = new ReadOnlyObservableCollection<IMapEntryBase>(new ObservableCollection<IMapEntryBase>(artistSongs));
            this.RaisePropertyChanged(nameof(Songs));
        }

        // Load the first song's cover as a fallback while we wait for the real artist image.
        // The fallback is only applied when no other image has been set yet.
        if (firstSong != null)
            _ = LoadFallbackCoverAsync(firstSong);

        // Check if Last.FM is available
        var lastFm = Locator.Current.GetService<ILastFmApiService>();
        IsLastFmAvailable = lastFm != null && lastFm.IsAuthorized();

        if (!IsLastFmAvailable) return;

        // Try loading from cache first
        var cached = await LoadCachedArtistInfoAsync(artistName);
        if (cached != null)
        {
            ApplyArtistInfo(cached);
            // Load image — try disk cache first, then fetch via MusicBrainz
            var cachedImageUrl = await LoadCachedStringAsync(artistName, "imageurl.txt");
            if (cachedImageUrl != null)
                _ = LoadBitmapFromCacheOrUrlAsync(artistName, cachedImageUrl);
            else if (cached.Artist?.Mbid != null)
                _ = FetchAndCacheImageAsync(artistName, cached.Artist.Mbid);

            var cachedSimilar = await LoadCachedSimilarArtistsAsync(artistName);
            if (cachedSimilar != null)
                ApplySimilarArtists(cachedSimilar);
            return;
        }

        // Fetch from Last.FM API
        IsLoading = true;

        try
        {
            var infoJson = await lastFm!.GetArtistInfoAsync(artistName);
            if (infoJson != null)
            {
                await SaveCacheAsync(artistName, "info.json", infoJson);
                var info = JsonSerializer.Deserialize<ArtistInfoResponse>(infoJson);
                if (info != null)
                {
                    ApplyArtistInfo(info);

                    // Kick off MusicBrainz image resolution if we have an MBID
                    if (info.Artist?.Mbid != null)
                        _ = FetchAndCacheImageAsync(artistName, info.Artist.Mbid);
                }
            }

            var similarJson = await lastFm.GetSimilarArtistsAsync(artistName);
            if (similarJson != null)
            {
                await SaveCacheAsync(artistName, "similar.json", similarJson);
                var similar = JsonSerializer.Deserialize<SimilarArtistsResponse>(similarJson);
                if (similar != null)
                    ApplySimilarArtists(similar);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load Last.FM data for '{artistName}': {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyArtistInfo(ArtistInfoResponse info)
    {
        if (info.Artist == null) return;

        HasLastFmData = true;

        if (info.Artist.Bio != null)
        {
            // Strip HTML tags from bio summary
            var summary = info.Artist.Bio.Summary ?? string.Empty;
            summary = System.Text.RegularExpressions.Regex.Replace(summary, "<.*?>", string.Empty).Trim();
            Biography = string.IsNullOrWhiteSpace(summary) ? null : summary;
        }

        Listeners = FormatStatNumber(info.Artist.Stats?.Listeners);
        PlayCount = FormatStatNumber(info.Artist.Stats?.Playcount);
        Tags = info.Artist.Tags?.Tag?.Select(t => t.Name ?? string.Empty).Where(t => !string.IsNullOrEmpty(t)).ToList();
    }

    private async void AddAllSongsToPlaylist(string name)
    {
        var playlist = _playlists?.FirstOrDefault(x => x.Name == name);
        if (playlist == null || Songs == null) return;

        foreach (var song in Songs)
            await PlaylistManager.AddSongToPlaylistAsync(playlist, song);

        Player.TriggerPlaylistChanged(new PropertyChangedEventArgs(name));
    }

    private void ApplySimilarArtists(SimilarArtistsResponse similar)
    {
        if (similar.SimilarArtists?.Artist == null) return;

        var allSongs = Player.SongSourceProvider.SongSourceList;

        SimilarArtists = similar.SimilarArtists.Artist
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .Select(a =>
            {
                var inLibrary = allSongs?.Any(s =>
                    string.Equals(s.Artist, a.Name, StringComparison.OrdinalIgnoreCase)) ?? false;
                return new SimilarArtistDisplayEntry(a.Name!, inLibrary);
            })
            .OrderByDescending(a => a.InLibrary)
            .ToList();
    }

    #region Cache

    private const int CacheDays = 30;
    private static readonly string CacheDir = Path.Combine("data", "artist_cache");

    private static string GetCacheFilePath(string artistName, string fileName)
    {
        // Use a hash of the lower-cased artist name to produce a safe, stable filename stem
        using var md5 = MD5.Create();
        var hash = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(artistName.ToLowerInvariant())))
            .Replace("-", "").ToLower();
        return Path.Combine(CacheDir, $"{hash}_{fileName}");
    }

    private static async Task SaveCacheAsync(string artistName, string fileName, string content)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            await File.WriteAllTextAsync(GetCacheFilePath(artistName, fileName), content);
        }
        catch
        {
            // Cache write failure is non-critical
        }
    }

    private static async Task<string?> LoadCachedStringAsync(string artistName, string fileName)
    {
        try
        {
            var path = GetCacheFilePath(artistName, fileName);
            if (!File.Exists(path)) return null;
            if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-CacheDays)) return null;

            var value = await File.ReadAllTextAsync(path);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tries to load the cover of <paramref name="song"/> and set it as <see cref="ArtistImage"/>
    /// only when no artist image has been found yet (i.e. <see cref="ArtistImage"/> is still null).
    /// </summary>
    private async Task LoadFallbackCoverAsync(IMapEntryBase song)
    {
        try
        {
            string? path = null;

            // RealmMapEntryBase exposes the background path directly
            if (song is RealmMapEntryBase realmEntry && !string.IsNullOrEmpty(realmEntry.BackgroundFileLocation)
                && File.Exists(realmEntry.BackgroundFileLocation))
            {
                path = realmEntry.BackgroundFileLocation;
            }
            else
            {
                // DbMapEntryBase requires reading the full entry and then resolving the background
                var fullEntry = await Task.Run(() => song.ReadFullEntry());
                if (fullEntry != null)
                    path = await fullEntry.FindBackground();
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            var bitmap = await Task.Run(() =>
            {
                try { return new Bitmap(path); }
                catch { return null; }
            });

            if (bitmap == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                // Only apply the fallback if a real artist image hasn't been loaded yet
                if (ArtistImage != null)
                {
                    bitmap.Dispose();
                    return;
                }

                ArtistImage = bitmap;
            });
        }
        catch
        {
            // Fallback is non-critical
        }
    }

    /// <summary>
    /// Resolves an artist image via MusicBrainz + Wikidata, downloads the bytes, caches them to disk,
    /// and sets ArtistImage. Fire-and-forget.
    /// </summary>
    private async Task FetchAndCacheImageAsync(string artistName, string mbid)
    {
        try
        {
            var url = await MusicBrainzImageResolver.GetArtistImageUrlAsync(mbid);
            if (url == null) return;

            // Save the URL so we know where to re-download from if the image cache expires
            await SaveCacheAsync(artistName, "imageurl.txt", url);

            await DownloadAndCacheImageAsync(artistName, url);
        }
        catch
        {
            // Non-critical
        }
    }

    /// <summary>
    /// Loads the artist Bitmap from the local image cache file if fresh, otherwise downloads
    /// from the given URL, saves to disk, and sets ArtistImage.
    /// </summary>
    private async Task LoadBitmapFromCacheOrUrlAsync(string artistName, string url)
    {
        try
        {
            var imagePath = GetCacheFilePath(artistName, "image.png");

            if (File.Exists(imagePath) &&
                File.GetLastWriteTimeUtc(imagePath) >= DateTime.UtcNow.AddDays(-CacheDays))
            {
                // Load directly from disk
                SetBitmapFromFile(imagePath);
                return;
            }

            // Disk copy is missing or stale — re-download
            await DownloadAndCacheImageAsync(artistName, url);
        }
        catch
        {
            // Non-critical
        }
    }

    private async Task DownloadAndCacheImageAsync(string artistName, string url)
    {
        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "MusicPlayerForOsu/1.0 (https://github.com/Christopher-Hayes/osuplayer)");
            var bytes = await http.GetByteArrayAsync(url);

            var imagePath = GetCacheFilePath(artistName, "image.png");
            Directory.CreateDirectory(CacheDir);
            await File.WriteAllBytesAsync(imagePath, bytes);

            SetBitmapFromFile(imagePath);
        }
        catch
        {
            // Non-critical
        }
    }

    private void SetBitmapFromFile(string path)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var bmp = new Bitmap(path);
                ArtistImage?.Dispose();
                ArtistImage = bmp;
            }
            catch
            {
                // Corrupt file — ignore
            }
        });
    }

    private static async Task<ArtistInfoResponse?> LoadCachedArtistInfoAsync(string artistName)
    {
        try
        {
            var json = await LoadCachedStringAsync(artistName, "info.json");
            return json == null ? null : JsonSerializer.Deserialize<ArtistInfoResponse>(json);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<SimilarArtistsResponse?> LoadCachedSimilarArtistsAsync(string artistName)
    {
        try
        {
            var json = await LoadCachedStringAsync(artistName, "similar.json");
            return json == null ? null : JsonSerializer.Deserialize<SimilarArtistsResponse>(json);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    private static string? FormatStatNumber(string? raw)
    {
        if (raw is null) return null;
        return long.TryParse(raw, out var n)
            ? n.ToString("N0")
            : raw;
    }
}

public record SimilarArtistDisplayEntry(string Name, bool InLibrary)
{
    public double Opacity => InLibrary ? 1.0 : 0.35;
}
