using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
using ReactiveUI;
using Splat;

namespace OsuPlayer.Windows;

public partial class FluentAppWindow : FluentReactiveWindow<FluentAppWindowViewModel>
{
    private readonly ILoggingService _loggingService;

    public Miniplayer? Miniplayer;
    public FullscreenWindow? FullscreenWindow;

    public FluentAppWindow() : this(Locator.Current.GetRequiredService<FluentAppWindowViewModel>(), Locator.Current.GetRequiredService<ILoggingService>())
    {
    }

    public FluentAppWindow(FluentAppWindowViewModel viewModel, ILoggingService loggingService)
    {
        ViewModel = viewModel;

        _loggingService = loggingService;

        var player = ViewModel.Player;

        InitializeComponent();

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

        this.WhenActivated(_ =>
        {
            if (ViewModel == null) return;

            ViewModel.MainView = ViewModel.HomeView;

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
            });

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
        config.Container.SelectedPlaylist = ViewModel.Player.SelectedPlaylist.Value?.Id;

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

            for (var i = 0; i < 4096; i++)
            {
                ViewModel.AudioVisualizer.SeriesValues[i] = new ObservableValue(0);
            }
        }
    }
}