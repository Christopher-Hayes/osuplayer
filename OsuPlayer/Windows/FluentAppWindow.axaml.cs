using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using LiveChartsCore.Defaults;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.Data.OsuPlayer.Enums;
using OsuPlayer.Extensions.EnumExtensions;
using OsuPlayer.Interfaces.Service;
using OsuPlayer.IO.Importer;
using OsuPlayer.Modules.Audio.Interfaces;
using OsuPlayer.Network;
using OsuPlayer.Services;
using OsuPlayer.Styles;
using OsuPlayer.UI_Extensions;
using OsuPlayer.Views;
using System.Reactive.Disposables;
using ReactiveUI;
using Splat;

namespace OsuPlayer.Windows;

public partial class FluentAppWindow : FluentReactiveWindow<FluentAppWindowViewModel>
{
    private readonly ILoggingService _loggingService;

    /// <summary>The bitmap currently visible in the BgNew* slot (owned here for disposal).</summary>
    private Bitmap? _currentBgBitmap;

    /// <summary>Cancels any in-progress background crossfade so a new one can start immediately.</summary>
    private CancellationTokenSource? _bgCts;

    public Miniplayer? Miniplayer;
    public FullscreenWindow? FullscreenWindow;

    #pragma warning disable CS0618 // Designer-only constructor
    public FluentAppWindow() : this(
        Locator.Current.GetService<FluentAppWindowViewModel>() ?? new FluentAppWindowViewModel(),
        Locator.Current.GetService<ILoggingService>()!)
    {
    }
    #pragma warning restore CS0618

    public FluentAppWindow(FluentAppWindowViewModel viewModel, ILoggingService loggingService)
    {
        ViewModel = viewModel;

        _loggingService = loggingService;

        InitializeComponent();

        AppNavigationView.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Grid>("PaneToggleButtonGrid") is { } grid)
                grid.Margin = new Thickness(12, 0, 0, 0);
        };

        var player = ViewModel?.Player;
        if (player is null) return; // Design-time: skip runtime wiring

        InitializeFluentAppWindow(player);

        // Wire up artwork overlay dismiss handlers
        ArtworkOverlayBackdrop.PointerPressed += (_, _) => DismissArtworkOverlay();
        ArtworkOverlayCloseButton.Click += (_, _) => DismissArtworkOverlay();
    }

    private void DismissArtworkOverlay()
    {
        if (ViewModel?.PlayerControl != null)
            ViewModel.PlayerControl.IsArtworkOverlayVisible = false;
    }

    private void InitializeFluentAppWindow(IPlayer player)
    {
        Task.Run(() => SongImporter.ImportSongsAsync(player.SongSourceProvider, player as IImportNotifications));

        // Setting AppWindow Properties
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = FATitleBarHitTestType.Complex;

        // Loading config stuff

        using (var config = new Config())
        {
            _loggingService.Log("Loaded config successfully", LogType.Success, config.Container);

            TransparencyLevelHint = config.Container.BackgroundMode switch
            {
                BackgroundMode.SolidColor => new[]
                {
                    WindowTransparencyLevel.None
                },
                BackgroundMode.AcrylicBlur => new[]
                {
                    WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None
                },
                BackgroundMode.Mica => new[]
                {
                    WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None
                },
                _ => new[]
                {
                    WindowTransparencyLevel.None
                },
            };

            TransparencyLevelHint = new[]
            {
                WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None
            };

            SetRenderMode(config.Container.RenderingMode);

            AppNavigationView.PaneDisplayMode = config.Container.UseLeftNavigationPosition ? FANavigationViewPaneDisplayMode.Left : FANavigationViewPaneDisplayMode.Top;

            var backgroundColor = config.Container.BackgroundColor;
            ViewModel!.DisplayBackgroundImage = config.Container.DisplayBackgroundImage;
            ViewModel.BackgroundBlurRadius = config.Container.BackgroundBlurRadius;

            Background = new SolidColorBrush(backgroundColor.ToColor());

            var accentColor = config.Container.AccentColor;

            ColorSetter.SetColor(accentColor.ToColor());

            Application.Current!.Resources["SmallerFontWeight"] = config.Container.GetNextSmallerFont().ToFontWeight();
            Application.Current!.Resources["DefaultFontWeight"] = config.Container.DefaultFontWeight.ToFontWeight();
            Application.Current!.Resources["BiggerFontWeight"] = config.Container.GetNextBiggerFont().ToFontWeight();

            // Disabled for now
            // FontFamily = config.Container.Font ?? FontManager.Current.DefaultFontFamily;
            // config.Container.Font ??= FontFamily.Name;

            // Restore window size and state from last session
            Width = config.Container.WindowWidth > 0 ? config.Container.WindowWidth : 1280;
            Height = config.Container.WindowHeight > 0 ? config.Container.WindowHeight : 720;
            var savedState = (WindowState)config.Container.WindowState;
            // Never restore Minimized or FullScreen on startup
            WindowState = savedState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
        }

        // Setting up last.fm stuff if enabled
        Task.Run(async () =>
        {
            try
            {
                var window = Locator.Current.GetService<FluentAppWindow>();
                var lastFmApi = Locator.Current.GetService<ILastFmApiService>();
                var loggingService = Locator.Current.GetService<ILoggingService>();

                await using var config = new Config();

                var apiKey = config.Container.LastFmApiKey;
                var apiSecret = config.Container.LastFmSecret;
                var sessionKey = await lastFmApi.LoadSessionKeyAsync();

                if (!string.IsNullOrWhiteSpace(apiKey) || !string.IsNullOrWhiteSpace(apiSecret) || !sessionKey)
                {
                    loggingService.Log("Can't connect to last.fm, because no apikey, apisecret or session key fast found", LogType.Warning);
                    return;
                }

                // We only load the APIKey from the config, as it is the only key that we save
                // 1. Because we always need the api key for all the request
                // 2. The secret is only used for the first authentication of the token
                // 3. After that all subsequent last.fm api calls only need the api key and session key
                lastFmApi.SetApiKeyAndSecret(apiKey, apiSecret);

                if (!lastFmApi.IsAuthorized())
                {
                    await lastFmApi.GetAuthToken();
                    lastFmApi.AuthorizeToken();

                    await MessageBox.ShowDialogAsync(window, "Close this window, when you are done, authenticating in the browser");

                    await lastFmApi.GetSessionKey();

                    await lastFmApi.SaveSessionKeyAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Something wrong happened when connecting to last.fm API {ex}");
            }
        });

        this.WhenActivated(disposables =>
        {
            if (ViewModel == null) return;

            using var startupConfig = new Config();
            var lastView = startupConfig.Container.LastActiveView;

            if (lastView != null && lastView.StartsWith("artist:"))
            {
                var artistName = lastView.Substring("artist:".Length);
                ViewModel.MainView = ViewModel.ArtistView;

                // Defer the actual artist load until songs have finished importing,
                // otherwise the song list and cover image will be empty.
                var importNotifications = (IImportNotifications)ViewModel.Player;
                importNotifications.SongsLoading
                    .BindValueChanged(e =>
                    {
                        if (e.NewValue) return; // still loading
                        Dispatcher.UIThread.Post(() => _ = ViewModel.ArtistView.LoadArtistAsync(artistName));
                    }, true);
            }
            else
            {
                ViewModel.MainView = lastView switch
                {
                    "ArtistsNavigation"  => (BaseViewModel)ViewModel.ArtistsView,
                    "PlaylistNavigation" => ViewModel.PlaylistView,
                    "SettingsNavigation" => ViewModel.SettingsView,
                    "SearchNavigation"   => ViewModel.SearchView,
                    _                    => ViewModel.HomeView
                };
            }

            // Keep the nav sidebar selection in sync when navigation happens programmatically
            // (e.g. clicking song name / artist / playlist label in the player bar).
            ViewModel.WhenAnyValue(x => x.MainView).Subscribe(view =>
            {
                var tag = view switch
                {
                    HomeViewModel     => "HomeNavigation",
                    ArtistsViewModel  => "ArtistsNavigation",
                    ArtistViewModel   => "ArtistsNavigation",
                    PlaylistViewModel => "PlaylistNavigation",
                    SettingsViewModel => "SettingsNavigation",
                    SearchViewModel   => null,
                    _                 => null
                };

                // Find the matching nav item and select it; clear selection for views with no nav entry.
                var match = AppNavigationView.MenuItems
                    .OfType<FANavigationViewItem>()
                    .Concat(AppNavigationView.FooterMenuItems.OfType<FANavigationViewItem>())
                    .FirstOrDefault(item => item.Tag as string == tag);

                AppNavigationView.SelectedItem = match;
            }).DisposeWith(disposables);

            // Crossfade the background image whenever the current song changes.
            ViewModel.WhenAnyValue(x => x.BackgroundImage)
                .Subscribe(newBitmap => _ = CrossfadeBackgroundAsync(newBitmap))
                .DisposeWith(disposables);

            using var config = new Config();

            SetAudioVisualization(config.Container.DisplayAudioVisualizer);
        });
    }

    private void AppNavigationView_OnItemInvoked(object? sender, FANavigationViewItemInvokedEventArgs e)
    {
        if (e.IsSettingsInvoked)
        {
            ViewModel!.MainView = ViewModel.SettingsView;

            return;
        }

        switch (e.InvokedItemContainer.Tag)
        {
            case "SearchNavigation":
            {
                ViewModel!.MainView = ViewModel.SearchView;
                break;
            }
            case "PlaylistNavigation":
            {
                ViewModel!.MainView = ViewModel.PlaylistView;
                break;
            }
            case "HomeNavigation":
            {
                ViewModel!.MainView = ViewModel.HomeView;
                break;
            }
            case "ArtistsNavigation":
            {
                ViewModel!.MainView = ViewModel.ArtistsView;
                break;
            }
            case "MiniplayerNavigation":
            {
                OpenMiniplayer();
                break;
            }
            case "SettingsNavigation":
                ViewModel!.MainView = ViewModel.SettingsView;
                break;
            default:
            {
                ViewModel!.MainView = ViewModel!.HomeView;
                break;
            }
        }
    }

    private async void SearchBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == default) return;

        var acb = sender as AutoCompleteBox;
        if (acb?.SelectedItem is not IMapEntryBase map) return;

        // Clear the box immediately so the dropdown closes and the field is reset
        acb.Text = null;

        // Play the song
        ViewModel.Player.ActivePlaylistContext.Value = null;
        ViewModel.Player.ActiveArtistContext.Value = null;
        await ViewModel.Player.TryPlaySongAsync(map);

        // Navigate to Home and scroll to the song
        ViewModel.MainView = ViewModel.HomeView;
        ViewModel.HomeView.SelectedSong = map;
    }

    private async void SearchBox_OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (ViewModel == default || e.Key != Key.Enter) return;

        var acb = (sender as AutoCompleteBox);

        if (acb?.SelectedItem is IMapEntryBase map)
        {
            var result = await ViewModel.Player.TryPlaySongAsync(map);

            if (result)
            {
                acb.Text = null;
                return;
            }
        }

        e.Handled = true;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (ViewModel == default) return;

        using var config = new Config();

        config.Container.Volume = ViewModel.Player.Volume.Value;
        config.Container.RepeatMode = ViewModel.Player.RepeatMode.Value;
        config.Container.IsShuffle = ViewModel.Player.IsShuffle.Value;
        config.Container.PlaybackSpeed = ViewModel.PlayerControl.PlaybackSpeed;
        config.Container.SelectedPlaylist = ViewModel.Player.SelectedPlaylist.Value?.Id;
        config.Container.ActivePlaylistContextId = ViewModel.Player.ActivePlaylistContext.Value?.Id;
        config.Container.LastActiveArtist = ViewModel.Player.ActiveArtistContext.Value;

        config.Container.LastActiveView = ViewModel.MainView switch
        {
            HomeViewModel     => "HomeNavigation",
            ArtistsViewModel  => "ArtistsNavigation",
            ArtistViewModel a => $"artist:{a.ArtistName}",
            PlaylistViewModel => "PlaylistNavigation",
            SettingsViewModel => "SettingsNavigation",
            SearchViewModel   => "SearchNavigation",
            _                 => "HomeNavigation"
        };

        // Persist window size/state so it can be restored on next launch
        config.Container.WindowState = (int)WindowState;
        if (WindowState == WindowState.Normal)
        {
            config.Container.WindowWidth = Width;
            config.Container.WindowHeight = Height;
        }

        ViewModel.Player.DisposeDiscordClient();
    }

    private async void Window_OnOpened(object? sender, EventArgs e)
    {
        if (Debugger.IsAttached) return;

        if (ViewModel == default) return;

        if (Directory.Exists("update_temp"))
        {
            await CleanupAfterUpdate();
        }

        await using var config = new Config();

        var result = await GitHub.CheckForUpdates(config.Container.ReleaseChannel);

        if (!result.IsNewVersionAvailable) return;

        ViewModel.UpdateView.Update = result;
        ViewModel.MainView = ViewModel.UpdateView;
    }

    private async Task CleanupAfterUpdate()
    {
        List<string> remainingUpdateFiles = Directory.EnumerateFiles("update_temp").ToList();

        if (remainingUpdateFiles.Any(x => !x.Contains("OsuPlayer.Updater")))
        {
            await MessageBox.ShowDialogAsync(this,
                "The remaining update files contain files that are not related to the updater. Please close the player and move the update files from 'update_temp' to the main directory manually and delete the 'update_temp' folder afterwards.");

            GeneralExtensions.OpenUrl($"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}");

            return;
        }

        foreach (string remainingUpdateFile in remainingUpdateFiles)
        {
            try
            {
                var newPath = Path.GetFileName(remainingUpdateFile);

                File.Delete(newPath);

                File.Move(remainingUpdateFile, newPath, true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"{remainingUpdateFile} has error with exception: {e.Message}");
                Console.WriteLine("Please restart the updater and make sure osu!player has quit.");

                Console.ReadKey();
                return;
            }
        }

        Directory.Delete("update_temp");
    }

    private void OpenMiniplayer()
    {
        if (ViewModel == default || Miniplayer != null)
            return;

        Miniplayer = new Miniplayer(ViewModel.Player, Locator.Current.GetRequiredService<IAudioEngine>());

        Miniplayer.Show();

        WindowState = WindowState.Minimized;
    }

    public void SetRenderMode(BitmapInterpolationMode renderMode)
    {
        RenderOptions.SetBitmapInterpolationMode(this, renderMode);
    }

    /// <summary>
    /// Crossfades the background from the previously displayed image to <paramref name="newBitmap"/>.
    /// The outgoing image fades from 0.25 → 0 while the incoming image fades from 0 → 0.25.
    /// Any in-progress crossfade is cancelled so fast song skipping stays responsive.
    /// </summary>
    private async Task CrossfadeBackgroundAsync(Bitmap? newBitmap)
    {
        // Cancel any in-progress fade so the new one starts immediately.
        _bgCts?.Cancel();
        _bgCts?.Dispose();
        var cts = new CancellationTokenSource();
        _bgCts = cts;

        var oldBitmap = _currentBgBitmap;
        _currentBgBitmap = newBitmap;

        // Snapshot the current displayed opacity of the BgNew* slot (may be mid-animation).
        var prevOpacity = BgNewAcrylic.Opacity;

        // Promote current → prev slot.
        BgPrevAcrylic.Source = BgNewAcrylic.Source;
        BgPrevLinux.Source   = BgNewLinux.Source;
        BgPrevAcrylic.Opacity = prevOpacity;
        BgPrevLinux.Opacity   = prevOpacity;

        // Load new image into the incoming slot, fully transparent.
        BgNewAcrylic.Source = newBitmap;
        BgNewLinux.Source   = newBitmap;
        BgNewAcrylic.Opacity = 0;
        BgNewLinux.Opacity   = 0;

        // Nothing to animate if both are empty.
        if (oldBitmap == null && newBitmap == null)
            return;

        const double TargetOpacity = 0.25;
        var duration = TimeSpan.FromMilliseconds(600);

        var fadeOut = new Animation
        {
            Duration = duration,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, prevOpacity) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, 0d) } }
            }
        };

        var targetOpacity = newBitmap != null ? TargetOpacity : 0d;
        var fadeIn = new Animation
        {
            Duration = duration,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, targetOpacity) } }
            }
        };

        try
        {
            await Task.WhenAll(
                fadeOut.RunAsync(BgPrevAcrylic, cts.Token),
                fadeOut.RunAsync(BgPrevLinux,   cts.Token),
                fadeIn.RunAsync(BgNewAcrylic,   cts.Token),
                fadeIn.RunAsync(BgNewLinux,     cts.Token)
            );
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cts.Token.IsCancellationRequested) return;

        // Settle final opacities and release the prev slot.
        BgNewAcrylic.Opacity  = targetOpacity;
        BgNewLinux.Opacity    = targetOpacity;
        BgPrevAcrylic.Opacity = 0;
        BgPrevLinux.Opacity   = 0;
        BgPrevAcrylic.Source  = null;
        BgPrevLinux.Source    = null;

        // Dispose the bitmap that was showing before this transition.
        oldBitmap?.Dispose();
    }

    public void SetAudioVisualization(bool value)
    {
        if (ViewModel == default) return;

        if (value)
        {
            ViewModel.AudioVisualizer.AudioVisualizerUpdateTimer.Start();
        }
        else
        {
            ViewModel.AudioVisualizer.AudioVisualizerUpdateTimer.Stop();

            for (var i = 0; i < ViewModel.AudioVisualizer.SeriesValues.Count; i++)
            {
                ViewModel.AudioVisualizer.SeriesValues[i] = new ObservableValue(0);
            }
        }
    }
}