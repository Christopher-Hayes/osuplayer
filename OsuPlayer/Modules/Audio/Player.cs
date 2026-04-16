using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Nein.Extensions;
using Nein.Extensions.Exceptions;
using OsuPlayer.Data.DataModels.Extensions;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.Data.OsuPlayer.Classes;
using OsuPlayer.Data.OsuPlayer.Enums;
using OsuPlayer.Data.OsuPlayer.StorageModels;
using OsuPlayer.Interfaces.Service;
using OsuPlayer.IO.Importer;
using OsuPlayer.IO.Storage.Blacklist;
using OsuPlayer.IO.Storage.Playlists;
using OsuPlayer.Modules.Audio.Engine;
using OsuPlayer.Modules.Audio.Interfaces;

namespace OsuPlayer.Modules.Audio;

/// <summary>
/// This class is a wrapper for our <see cref="BassEngine" />.
/// You can play, pause, stop and etc. from this class. Custom logic should also be implemented here
/// </summary>
public class Player : IPlayer, IImportNotifications
{
    private readonly IAudioEngine _audioEngine;
    private readonly Stopwatch _currentSongTimer = new();
    private readonly IDiscordService? _discordService;
    private readonly IShuffleServiceProvider? _shuffleProvider;
    private readonly IHistoryProvider? _historyProvider;
    private readonly WindowsMediaTransportControls? _winMediaControls;
    private readonly LinuxMprisService? _linuxMprisService;
    private readonly ILastFmApiService? _lastFmApi;

    private bool _isMuted;
    private double _oldVolume;

    public ISongSourceProvider SongSourceProvider { get; }
    public Bindable<bool> SongsLoading { get; } = new();

    public Bindable<IMapEntry?> CurrentSong { get; } = new();
    public Bindable<string?> CurrentSongImage { get; } = new();

    public Bindable<bool> IsPlaying { get; } = new();
    public Bindable<bool> IsShuffle { get; } = new();
    public Bindable<bool> BlacklistSkip { get; } = new();
    public Bindable<bool> PlaylistEnableOnPlay { get; } = new();
    public Bindable<RepeatMode> RepeatMode { get; } = new();

    public BindableList<HistoricalMapEntry> History { get; } = new();

    public List<AudioDevice> AvailableAudioDevices => _audioEngine.AvailableAudioDevices;
    public BindableArray<decimal> EqGains => _audioEngine.EqGains;
    public Bindable<double> Volume => _audioEngine.Volume;

    public int CurrentIndex { get; private set; }

    public bool IsEqEnabled
    {
        get => _audioEngine.IsEqEnabled;
        set => _audioEngine.IsEqEnabled = value;
    }

    public Bindable<Playlist?> SelectedPlaylist { get; } = new();
    public Bindable<Playlist?> ActivePlaylistContext { get; } = new();
    private List<IMapEntryBase> ActivePlaylistSongs { get; set; } = new();

    public Player(IAudioEngine audioEngine, ISongSourceProvider songSourceProvider, IShuffleServiceProvider? shuffleProvider = null,
        ISortProvider? sortProvider = null, IHistoryProvider? historyProvider = null,
        IDiscordService? discordService = null,
        ILastFmApiService? lastFmApi = null)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                _winMediaControls = new WindowsMediaTransportControls(this);
            }
            catch
            {
                _winMediaControls = null;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            try
            {
                _linuxMprisService = new LinuxMprisService(this);
                _ = Task.Run(() => _linuxMprisService.StartAsync());
            }
            catch
            {
                _linuxMprisService = null;
            }
        }

        SongSourceProvider = songSourceProvider;

        _audioEngine = audioEngine;
        _audioEngine.ChannelReachedEnd = async () => await NextSong(PlayDirection.Forward);
        _shuffleProvider = shuffleProvider;
        _historyProvider = historyProvider;
        _lastFmApi = lastFmApi;
        _discordService = discordService;

        LoadPlayerConfiguration();

        InitPlayer(songSourceProvider);
    }

    private void LoadPlayerConfiguration()
    {
        var config = new Config();

        if (config.Container.UseDiscordRpc)
            _discordService?.Initialize();

        Volume.Value = config.Container.Volume;
        BlacklistSkip.Value = config.Container.BlacklistSkip;
        PlaylistEnableOnPlay.Value = config.Container.PlaylistEnableOnPlay;
        RepeatMode.Value = config.Container.RepeatMode;
        IsShuffle.Value = config.Container.IsShuffle;
    }

    private void InitPlayer(ISongSourceProvider songSourceProvider)
    {
        IsPlaying.BindTo(_audioEngine.IsPlaying);

        SongSourceProvider.Songs?.Subscribe(_ => CurrentIndex = songSourceProvider.SongSourceList?.IndexOf(CurrentSong.Value) ?? -1);

        CurrentSong.BindValueChanged(OnCurrentSongChanged, true);
        RepeatMode.BindValueChanged(OnRepeatModeChanged, true);
        IsShuffle.BindValueChanged(_ => _shuffleProvider?.ShuffleImpl?.Init(0));
        SelectedPlaylist.BindValueChanged(OnSelectedPlaylistChanged, true);
        ActivePlaylistContext.BindValueChanged(OnActivePlaylistContextChanged, true);
    }

    private async void OnSelectedPlaylistChanged(ValueChangedEvent<Playlist?> selectedPlaylist)
    {
        if (selectedPlaylist.NewValue == null) return;

        await using var cfg = new Config();

        cfg.Container.SelectedPlaylist = selectedPlaylist.NewValue.Id;
    }

    private async void OnActivePlaylistContextChanged(ValueChangedEvent<Playlist?> playlistContext)
    {
        if (playlistContext.NewValue == null)
        {
            ActivePlaylistSongs = new List<IMapEntryBase>();
            return;
        }

        ActivePlaylistSongs = SongSourceProvider.GetMapEntriesFromHash(playlistContext.NewValue.Songs, out var invalidHashes);

        if (invalidHashes.Any())
        {
            using var playlists = new PlaylistStorage();

            var playlist = playlists.Container.Playlists?.First(x => x.Id == playlistContext.NewValue.Id);

            playlist?.Songs.RemoveWhere(song => invalidHashes.Contains(song));
        }

        if (CurrentSong.Value == null) return;

        if (!ActivePlaylistSongs.Contains(CurrentSong.Value)) await NextSong(PlayDirection.Forward);
    }

    private void OnRepeatModeChanged(ValueChangedEvent<RepeatMode> repeatMode)
    {
        using var cfg = new Config();

        cfg.Container.RepeatMode = repeatMode.NewValue;
    }

    private void OnCurrentSongChanged(ValueChangedEvent<IMapEntry> mapEntry)
    {
        _historyProvider?.AddOrUpdateMapEntry(mapEntry.NewValue);

        using var cfg = new Config();

        cfg.Container.LastPlayedSong = mapEntry.NewValue?.Hash;

        if (mapEntry.NewValue is null) return;

        var timestamp = TimeSpan.FromSeconds(_audioEngine.ChannelLength.Value * (1 - _audioEngine.PlaybackSpeed.Value));

        _discordService?.UpdatePresence(mapEntry.NewValue.Title, $"by {mapEntry.NewValue.Artist}", mapEntry.NewValue.BeatmapSetId, durationLeft: timestamp);
    }

    public void OnImportStarted()
    {
        SongsLoading.Value = true;
    }

    public async void OnImportFinished(bool success)
    {
        SongsLoading.Value = false;

        if (!success)
            return;

        var config = new Config();
        var playlists = new PlaylistStorage();

        SelectedPlaylist.Value = playlists.Container.Playlists?.FirstOrDefault(x => x.Id == config.Container.SelectedPlaylist) ??
                                 playlists.Container.Playlists?.First(y => y.Name == "Favorites");

        switch (config.Container.StartupSong)
        {
            case StartupSong.FirstSong:
                await TryPlaySongAsync(SongSourceProvider.SongSourceList?[0]);
                break;
            case StartupSong.LastPlayed:
                await PlayLastPlayedSongAsync(config.Container);
                break;
            case StartupSong.RandomSong:
                await TryPlaySongAsync(SongSourceProvider.SongSourceList?[new Random().Next(SongSourceProvider.SongSourceList.Count)]);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Startup song type {config.Container.StartupSong} is not supported!");
        }
    }

    public event PropertyChangedEventHandler? PlaylistChanged;
    public event PropertyChangedEventHandler? BlacklistChanged;

    public void SetDevice(AudioDevice audioDevice)
    {
        _audioEngine.SetDevice(audioDevice);
    }

    public void TriggerPlaylistChanged(PropertyChangedEventArgs e)
    {
        PlaylistChanged?.Invoke(this, e);
    }

    public void TriggerBlacklistChanged(PropertyChangedEventArgs e)
    {
        BlacklistChanged?.Invoke(this, e);
    }

    public void SetPlaybackSpeed(double speed)
    {
        _audioEngine.SetPlaybackSpeed(speed);

        if (!_audioEngine.IsPlaying.Value) return;

        var timestamp = TimeSpan.FromSeconds(_audioEngine.ChannelLength.Value * (1 - _audioEngine.PlaybackSpeed.Value) - _audioEngine.ChannelPosition.Value);

        _discordService?.UpdatePresence(CurrentSong.Value.Title, $"by {CurrentSong.Value.Artist}", CurrentSong.Value.BeatmapSetId, durationLeft: timestamp);
    }

    public void UpdatePlaybackMethod()
    {
        _audioEngine.UpdatePlaybackMethod();
    }

    public void DisposeDiscordClient()
    {
        _discordService?.DeInitialize();
    }

    public void PlayPause()
    {
        if (!IsPlaying.Value)
        {
            Play();
        }
        else
        {
            Pause();
        }
    }

    public void Play()
    {
        _audioEngine.Play();
        _currentSongTimer.Start();

        _winMediaControls?.UpdatePlayingStatus(true);
        _linuxMprisService?.UpdatePlaybackStatus(true);

        var timestamp = TimeSpan.FromSeconds(_audioEngine.ChannelLength.Value * (1 - _audioEngine.PlaybackSpeed.Value) - _audioEngine.ChannelPosition.Value);

        _discordService?.UpdatePresence(CurrentSong.Value.Title, $"by {CurrentSong.Value.Artist}", CurrentSong.Value.BeatmapSetId, durationLeft: timestamp);
    }

    public void Pause()
    {
        _audioEngine.Pause();
        _currentSongTimer.Stop();

        _winMediaControls?.UpdatePlayingStatus(false);
        _linuxMprisService?.UpdatePlaybackStatus(false);

        _discordService?.UpdatePresence(CurrentSong.Value.Title, $"by {CurrentSong.Value.Artist}", CurrentSong.Value.BeatmapSetId);
    }

    public void Stop()
    {
        _audioEngine.Stop();
    }

    public void ToggleMute()
    {
        if (!_isMuted)
        {
            _oldVolume = Volume.Value;
            _audioEngine.Volume.Value = 0;
            _isMuted = true;
        }
        else
        {
            Volume.Value = _oldVolume;
            _isMuted = false;
        }
    }

    public async Task<bool> NextSong(PlayDirection playDirection)
    {
        if (SongSourceProvider.SongSourceList == null || !SongSourceProvider.SongSourceList.Any())
            return false;

        if (playDirection == PlayDirection.Backwards && _audioEngine.ChannelPosition.Value > 3)
        {
            return await TryStartSongAsync(CurrentSong.Value ?? SongSourceProvider.SongSourceList[0]);
        }

        // Determine the active song source: playlist context if set, otherwise full library
        var songSource = ActivePlaylistContext.Value != null && ActivePlaylistSongs.Any()
            ? ActivePlaylistSongs
            : (IList<IMapEntryBase>) SongSourceProvider.SongSourceList;

        return RepeatMode.Value switch
        {
            Data.OsuPlayer.Enums.RepeatMode.NoRepeat => await TryPlaySongAsync(
                GetNextSongToPlay(songSource, CurrentIndex, playDirection), playDirection),
            Data.OsuPlayer.Enums.RepeatMode.RepeatAll => await TryPlaySongAsync(
                GetNextSongToPlay(songSource, CurrentIndex, playDirection), playDirection),
            Data.OsuPlayer.Enums.RepeatMode.RepeatOne => await TryStartSongAsync(CurrentSong.Value!),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public async Task<bool> TryPlaySongAsync(IMapEntryBase? song, PlayDirection playDirection = PlayDirection.Normal)
    {
        if (SongSourceProvider.SongSourceList == default || !SongSourceProvider.SongSourceList.Any())
            throw new NullOrEmptyException($"{nameof(SongSourceProvider.SongSourceList)} can't be null or empty");
        if (song == default)
        {
            return await TryStartSongAsync(SongSourceProvider.SongSourceList[0]);
        }

        if ((await new Config().ReadAsync()).IgnoreSongsWithSameNameCheckBox &&
            string.Equals(CurrentSong.Value?.SongName, song.SongName, StringComparison.OrdinalIgnoreCase))
        {
            switch (playDirection)
            {
                case PlayDirection.Forward:
                case PlayDirection.Backwards:
                    CurrentIndex += (int) playDirection;
                    return await NextSong(playDirection);
                default:
                    return await TryStartSongAsync(song);
            }
        }

        return await TryStartSongAsync(song);
    }

    /// <summary>
    /// Plays the last played song read from the <see cref="ConfigContainer" /> and defaults to the
    /// first song in the <see cref="ISongSourceProvider.SongSourceList" /> if null
    /// </summary>
    /// <param name="config">optional parameter defaults to null. Used to avoid duplications of config instances</param>
    private async Task PlayLastPlayedSongAsync(ConfigContainer? config = null)
    {
        config ??= new Config().Container;

        if (config.LastPlayedSong == null)
        {
            await TryPlaySongAsync(null);
            return;
        }

        if (!string.IsNullOrWhiteSpace(config.LastPlayedSong))
        {
            await TryPlaySongAsync(SongSourceProvider.GetMapEntryFromHash(config.LastPlayedSong));
            return;
        }

        await TryPlaySongAsync(SongSourceProvider.SongSourceList?[0]);
    }

    private IMapEntryBase GetNextSongToPlay(IList<IMapEntryBase> songSource, int currentIndex, PlayDirection playDirection)
    {
        IMapEntryBase songToPlay;

        var offset = (int) playDirection;

        if (SongSourceProvider.SongSourceList == null || !SongSourceProvider.SongSourceList.Any())
            throw new NullOrEmptyException($"{nameof(SongSourceProvider.SongSourceList)} can't be null or empty");

        if (!SongSourceProvider.SongSourceList.IsInBounds(currentIndex))
            currentIndex = 0;

        currentIndex = songSource.IndexOf(SongSourceProvider.SongSourceList[currentIndex]);

        if (!songSource.Any())
        {
            RepeatMode.Value = Data.OsuPlayer.Enums.RepeatMode.NoRepeat;

            return SongSourceProvider.SongSourceList[0];
        }

        if (IsShuffle.Value && _shuffleProvider?.ShuffleImpl != null)
        {
            // When going backwards in shuffle mode, navigate to the previously played song via
            // the play history instead of picking a new random song.
            if (playDirection == PlayDirection.Backwards && _historyProvider != null)
            {
                var history = _historyProvider.History;
                var currentHash = CurrentSong.Value?.Hash;
                var currentHistoryIndex = -1;

                for (var i = history.Count - 1; i >= 0; i--)
                {
                    if (history[i].MapEntry.Hash == currentHash)
                    {
                        currentHistoryIndex = i;
                        break;
                    }
                }

                if (currentHistoryIndex > 0)
                {
                    var prevSong = songSource.FirstOrDefault(s => s.Hash == history[currentHistoryIndex - 1].MapEntry.Hash);
                    if (prevSong != null)
                        return prevSong;
                }
            }

            _shuffleProvider.ShuffleImpl.Init(songSource.Count);

            songToPlay = songSource[_shuffleProvider.ShuffleImpl.DoShuffle(currentIndex, (ShuffleDirection) playDirection)];
        }
        else
        {
            var x = (currentIndex + offset) % songSource!.Count;
            currentIndex = x < 0 ? x + songSource!.Count : x;

            songToPlay = songSource[currentIndex];
        }

        if (BlacklistSkip.Value && new Blacklist().Container.Songs.Contains(songToPlay.Hash))
            songToPlay = GetNextSongToPlay(songSource, currentIndex, playDirection);

        return songToPlay;
    }

    /// <summary>
    /// Starts playing a song
    /// </summary>
    /// <param name="song">a <see cref="IMapEntryBase" /> to play next</param>
    /// <returns>a <see cref="Task" /> with the resulting state</returns>
    private async Task<bool> TryStartSongAsync(IMapEntryBase song)
    {
        if (SongSourceProvider.SongSourceList == null || !SongSourceProvider.SongSourceList.Any())
            throw new NullOrEmptyException($"{nameof(SongSourceProvider.SongSourceList)} can't be null or empty");

        await using var config = new Config();

        var fullMapEntry = song.ReadFullEntry();

        if (fullMapEntry == default)
        {
            return await NextSong(PlayDirection.Forward);
        }

        fullMapEntry.UseUnicode = config.Container.UseSongNameUnicode;

        var findBackgroundTask = fullMapEntry.FindBackground();

        // Capture the previous song and how long it was listened to, before the timer is reset.
        // These values are used later to decide whether to scrobble the previous song.
        var previousSong = CurrentSong.Value;
        var previousSongElapsedMs = _currentSongTimer.ElapsedMilliseconds;
        var previousSongLengthMs = _audioEngine.ChannelLength.Value * 1000.0;

        _currentSongTimer.Stop();

        CurrentSongImage.Value = await findBackgroundTask;

        try
        {
            _audioEngine.OpenFile(fullMapEntry.FullPath!);
            _audioEngine.Play();

            _winMediaControls?.UpdatePlayingStatus(true);
            _winMediaControls?.SetMetadata(fullMapEntry);

            _linuxMprisService?.UpdatePlaybackStatus(true);
            _linuxMprisService?.UpdateMetadata(fullMapEntry, CurrentSongImage.Value);

            _currentSongTimer.Restart();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
            return false;
        }

        CurrentSong.Value = fullMapEntry;
        CurrentIndex = SongSourceProvider.SongSourceList.IndexOf(fullMapEntry);

        await UpdateTrackMetadataAndScrobble(config, previousSong, previousSongElapsedMs, previousSongLengthMs);

        return true;
    }

    private async Task UpdateTrackMetadataAndScrobble(Config config, IMapEntry? previousSong = null, long previousSongElapsedMs = 0, double previousSongLengthMs = 0)
    {
        try
        {
            if (!config.Container.EnableScrobbling)
                return;

            if (previousSong == null
                || string.IsNullOrWhiteSpace(previousSong.GetTitle())
                || string.IsNullOrWhiteSpace(previousSong.GetArtist()))
                return;

            // Per the Last.fm scrobbling spec, a track should only be scrobbled when:
            //   - It has been played for at least 30 seconds, AND
            //   - Either half the track has been played, or 4 minutes have elapsed
            //     (whichever comes first).
            // This prevents rapid song skips from all being scrobbled.
            const long minScrobbleMs = 30_000;  // 30 seconds
            const long maxScrobbleMs = 240_000; // 4 minutes

            if (previousSongElapsedMs < minScrobbleMs)
                return;

            if (previousSongLengthMs > 0
                && previousSongElapsedMs < previousSongLengthMs / 2
                && previousSongElapsedMs < maxScrobbleMs)
                return;

            await _lastFmApi?.Scrobble(previousSong.Title, previousSong.Artist)!;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Could not update last.fm error => {e}");
        }
    }
}