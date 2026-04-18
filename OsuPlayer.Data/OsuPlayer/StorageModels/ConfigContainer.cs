using Avalonia.Media.Imaging;
using OsuPlayer.Data.OsuPlayer.Enums;

namespace OsuPlayer.Data.OsuPlayer.StorageModels;

public class ConfigContainer : IStorableContainer
{
    public string? OsuPath { get; set; }
    public double Volume { get; set; } = 50;
    public bool UseSongNameUnicode { get; set; } = false;
    public string? SelectedAudioDeviceDriver { get; set; }
    public bool IsEqEnabled { get; set; }
    public StartupSong StartupSong { get; set; } = StartupSong.FirstSong;
    public SortingMode SortingMode { get; set; } = SortingMode.Title;
    public RepeatMode RepeatMode { get; set; } = RepeatMode.RepeatAll;
    public Guid? SelectedPlaylist { get; set; }
    public string? ShuffleAlgorithm { get; set; } = "RngShuffler";
    public bool IsShuffle { get; set; } = true;
    public string? LastPlayedSong { get; set; }
    public bool IgnoreSongsWithSameNameCheckBox { get; set; }
    public bool BlacklistSkip { get; set; }
    public bool PlaylistEnableOnPlay { get; set; }
    public string? Username { get; set; }
    public ReleaseChannels ReleaseChannel { get; set; } = 0;
    public KnownColors BackgroundColor { get; set; } = KnownColors.Black;
    public KnownColors AccentColor { get; set; } = KnownColors.White;
    public FontWeights DefaultFontWeight { get; set; } = FontWeights.Medium;
    public string? Font { get; set; }
    public bool UseDiscordRpc { get; set; }
    public bool UsePitch { get; set; } = true;
    public bool UseAudioNormalization { get; set; } = false;
    public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.AcrylicBlur;
    public float BackgroundBlurRadius { get; set; } = 50f;
    public bool DisplayBackgroundImage { get; set; } = true;
    public string LastFmApiKey { get; set; }
    public string LastFmSecret { get; set; }
    public bool EnableScrobbling { get; set; } = false;
    public bool DisplayerUserStats { get; set; } = true;
    public bool UseLeftNavigationPosition { get; set; } = true;
    public BitmapInterpolationMode RenderingMode { get; set; } = BitmapInterpolationMode.HighQuality;
    public bool DisplayAudioVisualizer { get; set; } = true;
    public bool DisplaySongListCovers { get; set; } = true;
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 720;
    /// <summary>Saved window state: 0 = Normal, 1 = Minimized, 2 = Maximized, 3 = FullScreen</summary>
    public int WindowState { get; set; } = 0;
    /// <summary>Playback speed offset (0.0 = 1x, 0.5 = 1.5x, -0.5 = 0.5x)</summary>
    public double PlaybackSpeed { get; set; } = 0.0;
    /// <summary>ID of the playlist that was active (playing from) when the app was last closed.</summary>
    public Guid? ActivePlaylistContextId { get; set; }
    /// <summary>Artist name that was active (playing from) when the app was last closed. Mutually exclusive with ActivePlaylistContextId.</summary>
    public string? LastActiveArtist { get; set; }
    /// <summary>Navigation tag of the view that was open when the app was last closed (e.g. "HomeNavigation").</summary>
    public string? LastActiveView { get; set; }

    public IStorableContainer Init()
    {
        return this;
    }
}