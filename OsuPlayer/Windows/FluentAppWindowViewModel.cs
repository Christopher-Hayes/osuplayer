using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.Interfaces.Service;
using OsuPlayer.Modules;
using OsuPlayer.Modules.Audio.Interfaces;
using OsuPlayer.Views;
using OsuPlayer.Views.CustomControls;
using ReactiveUI;
using Splat;

namespace OsuPlayer.Windows;

public class FluentAppWindowViewModel : BaseWindowViewModel
{
    public readonly IPlayer Player;

    private BaseViewModel? _mainView;
    private bool _displayBackgroundImage;
    private Bitmap? _backgroundImage;
    private float _backgroundBlurRadius;
    private bool _isCompactMode;

    public PlayerControlViewModel PlayerControl { get; }

    public HomeViewModel HomeView { get; }
    public BlacklistEditorViewModel BlacklistEditorView { get; }
    public PlaylistEditorViewModel PlaylistEditorView { get; }
    public PlaylistViewModel PlaylistView { get; }
    public SearchViewModel SearchView { get; }
    public SettingsViewModel SettingsView { get; }
    public UpdateViewModel UpdateView { get; }
    public EqualizerViewModel EqualizerView { get; }
    public ExportSongsViewModel ExportSongsView { get; }
    public PlayHistoryViewModel PlayHistoryView { get; }

    public ArtistsViewModel ArtistsView { get; }
    public ArtistViewModel ArtistView { get; }

    public AudioVisualizerViewModel AudioVisualizer { get; }

    public ExperimentalAcrylicMaterial? PanelMaterial { get; set; }

    public bool IsNonLinuxOs { get; }
    public bool IsLinuxOs { get; }

    private ReadOnlyObservableCollection<IMapEntryBase>? _songList;

    public ReadOnlyObservableCollection<IMapEntryBase>? SongList
    {
        get => _songList;
        set => this.RaiseAndSetIfChanged(ref _songList, value);
    }

    public bool DisplayBackgroundImage
    {
        get => _displayBackgroundImage;
        set => this.RaiseAndSetIfChanged(ref _displayBackgroundImage, value);
    }

    public float BackgroundBlurRadius
    {
        get => _backgroundBlurRadius;
        set => this.RaiseAndSetIfChanged(ref _backgroundBlurRadius, value);
    }

    public BaseViewModel? MainView
    {
        get => _mainView;
        set => this.RaiseAndSetIfChanged(ref _mainView, value);
    }

    public bool IsCompactMode
    {
        get => _isCompactMode;
        set => this.RaiseAndSetIfChanged(ref _isCompactMode, value);
    }

    public Bitmap? BackgroundImage
    {
        get => _backgroundImage;
        set => this.RaiseAndSetIfChanged(ref _backgroundImage, value);
    }

    /// <summary>
    /// Parameterless constructor for the AXAML designer / previewer.
    /// </summary>
    [Obsolete("Designer-only")]
    public FluentAppWindowViewModel() : this(null!, null!)
    {
    }

    public FluentAppWindowViewModel(IAudioEngine engine, IPlayer player, IShuffleServiceProvider? shuffleServiceProvider = null,
        ISortProvider? sortProvider = null, IHistoryProvider? historyProvider = null)
    {
        Player = player;

        // Design-time: skip service wiring
        if (player is null) return;

        AudioVisualizer = new AudioVisualizerViewModel(Locator.Current.GetRequiredService<IAudioEngine>());

        PlayerControl = new PlayerControlViewModel(Player, engine, this);

        SearchView = new SearchViewModel(Player);
        PlaylistView = new PlaylistViewModel(Player);
        PlaylistEditorView = new PlaylistEditorViewModel(Player);
        BlacklistEditorView = new BlacklistEditorViewModel(Player);
        HomeView = new HomeViewModel(Player);
        SettingsView = new SettingsViewModel(Player, sortProvider, shuffleServiceProvider);
        EqualizerView = new EqualizerViewModel(Player);
        UpdateView = new UpdateViewModel();
        ExportSongsView = new ExportSongsViewModel(Player.SongSourceProvider);
        PlayHistoryView = new PlayHistoryViewModel(Player, historyProvider, Player.SongSourceProvider);

        ArtistsView = new ArtistsViewModel(Player);
        ArtistView = new ArtistViewModel(Player);

        IsLinuxOs = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        IsNonLinuxOs = !IsLinuxOs;

        PanelMaterial = new ExperimentalAcrylicMaterial
        {
            BackgroundSource = AcrylicBackgroundSource.Digger,
            TintColor = Colors.Black,
            TintOpacity = 0.75,
            MaterialOpacity = 0.25
        };

        SongList = Player.SongSourceProvider.SongSourceList;

        Player.CurrentSongImage.BindValueChanged(d =>
        {
            var path = d.NewValue;

            // Defer to the UI thread so that properties like DisplayBackgroundImage
            // (set from config after the VM constructor) are already populated on the
            // initial fire (BindValueChanged with runOnceImmediately: true).
            Dispatcher.UIThread.Post(() =>
            {
                if (!DisplayBackgroundImage || string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    BackgroundImage = null;
                    return;
                }

                // Capture blur radius here (inside the Post) so config values are
                // guaranteed to be loaded — the VM constructor runs before config is applied.
                var blur = BackgroundBlurRadius;

                // Run the expensive blur on a background thread to avoid blocking the UI
                Task.Run(() =>
                {
                    try
                    {
                        var bmp = BitmapExtensions.BlurBitmap(path, blur, 1.0f, 40);
                        Dispatcher.UIThread.Post(() =>
                        {
                            // Don't dispose the old BackgroundImage here — CrossfadeBackgroundAsync
                            // owns the lifecycle of the outgoing bitmap and disposes it after the fade.
                            BackgroundImage = bmp;
                        });
                    }
                    catch
                    {
                        Dispatcher.UIThread.Post(() => BackgroundImage = null);
                    }
                });
            });
        }, true, true);
    }
}