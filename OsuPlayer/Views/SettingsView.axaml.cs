using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Extensions;
using OsuPlayer.Interfaces.Service;
using OsuPlayer.IO.Importer;
using OsuPlayer.Modules.Audio.Interfaces;
using OsuPlayer.UI_Extensions;
using OsuPlayer.Windows;
using ReactiveUI;
using Splat;

namespace OsuPlayer.Views;

public partial class SettingsView : ReactiveControl<SettingsViewModel>
{
    private readonly FluentAppWindow? _mainWindow;

    public SettingsView()
    {
        InitializeComponent();

        _mainWindow = Locator.Current.GetRequiredService<FluentAppWindow>();

        this.WhenActivated(_ =>
        {
            ViewModel.MainWindow = Locator.Current.GetRequiredService<FluentAppWindow>();

            ViewModel.SettingsCategories = SettingsGrid.Children;
        });
    }

    private TopLevel? GetTopLevel() => TopLevel.GetTopLevel(_mainWindow);

    private void SettingsView_OnInitialized(object? sender, EventArgs e)
    {
        var config = new Config();

        ViewModel!.OsuLocation = config.Container.OsuPath!;
    }

    public async void ImportSongsClick(object? sender, RoutedEventArgs routedEventArgs)
    {
        var topLevel = GetTopLevel();

        if (_mainWindow == default || topLevel == default) return;

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            Title = "Select your osu!.db or client.realm",
            AllowMultiple = false,
            FileTypeFilter = new ReadOnlyCollection<FilePickerFileType>(new List<FilePickerFileType>()
            {
                FilePickerFileTypesExtensions.OsuDb
            })
        });

        if (result.Any() == false)
        {
            await MessageBox.ShowDialogAsync(_mainWindow, "Did you even selected a file?!");
            return;
        }

        var dbFilePath = result.First().Path.AbsolutePath;

        if (Path.GetFileName(dbFilePath) != "osu!.db" && Path.GetFileName(dbFilePath) != "client.realm")
        {
            await MessageBox.ShowDialogAsync(_mainWindow, "You had one job! Just one. Select your osu!.db or client.realm! Not anything else!");
            return;
        }

        var osuFolder = Path.GetDirectoryName(dbFilePath);

        // Decode the path, so stuff like %20 are encoded properly
        osuFolder = HttpUtility.UrlDecode(osuFolder);

        await using (var config = new Config())
        {
            (await config.ReadAsync()).OsuPath = osuFolder!;
            ViewModel.OsuLocation = osuFolder!;
        }

        var player = ViewModel.Player;

        await Task.Run(() => SongImporter.ImportSongsAsync(player.SongSourceProvider, player as IImportNotifications));
        //await Task.Run(ViewModel.Player.ImportSongsAsync);
    }

    public async void ImportCollectionsClick(object? sender, RoutedEventArgs routedEventArgs)
    {
        if (_mainWindow == default) return;

        var player = ViewModel.Player;
        var success = await Task.Run(() => CollectionImporter.ImportCollectionsAsync(player.SongSourceProvider));

        MessageBox.Show(_mainWindow, success ? "Import successful. Have fun!" : "There are no collections in osu!", "Import complete!");
    }

    private void OpenEqClick(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow?.ViewModel == default) return;

        _mainWindow.ViewModel.MainView = _mainWindow.ViewModel.EqualizerView;
    }

    private void OnUsePitch_Click(object? sender, RoutedEventArgs e)
    {
        var value = (sender as ToggleSwitch)?.IsChecked;

        using var config = new Config();

        config.Container.UsePitch = value ?? true;

        var engine = Locator.Current.GetRequiredService<IAudioEngine>();

        engine.UpdatePlaybackMethod();
    }

    private async void LastFmAuth_OnClick(object? sender, RoutedEventArgs e)
    {
        var window = Locator.Current.GetService<FluentAppWindow>();
        var lastFmApi = Locator.Current.GetService<ILastFmApiService>();

        await using var config = new Config();

        var apiKey = string.IsNullOrWhiteSpace(ViewModel.LastFmApiKey) ? config.Container.LastFmApiKey : ViewModel.LastFmApiKey;
        var apiSecret = string.IsNullOrWhiteSpace(ViewModel.LastFmApiSecret) ? config.Container.LastFmSecret : ViewModel.LastFmApiSecret;

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            await MessageBox.ShowDialogAsync(window, "Please enter a API-Key and API-Secret before authorizing");
            return;
        }

        // Persist credentials to config immediately so they survive restarts even if
        // the auth flow below fails partway through.
        config.Container.LastFmApiKey = apiKey;
        config.Container.LastFmSecret = apiSecret;
        await config.SaveAsync(config.Container);

        lastFmApi.SetApiKeyAndSecret(apiKey, apiSecret);

        await lastFmApi.LoadSessionKeyAsync();

        // If a local session key exists, verify it's still accepted by Last.FM before
        // skipping the re-auth flow. This catches revoked/expired keys immediately
        // without waiting for the next scrobble to fail.
        if (lastFmApi.IsAuthorized())
            ViewModel.IsLastFmAuthorized = await lastFmApi.ValidateSessionAsync();
        else
            ViewModel.IsLastFmAuthorized = false;

        if (!ViewModel.IsLastFmAuthorized)
        {
            await lastFmApi.GetAuthToken();
            lastFmApi.AuthorizeToken();

            await MessageBox.ShowDialogAsync(window,
                "A Last.FM authorization page has been opened in your browser.\n\nGo to that tab, log in, and approve access for osu!player. Once you've clicked \"Allow\" in the browser, come back here and click OK.",
                "Authorize Last.FM");

            await lastFmApi.GetSessionKey();

            await lastFmApi.SaveSessionKeyAsync();

            ViewModel.IsLastFmAuthorized = lastFmApi.IsAuthorized();
        }
    }

    private void ExportCollectionsClick(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow?.ViewModel == null) return;

        _mainWindow.ViewModel.MainView = _mainWindow.ViewModel.ExportSongsView;
    }

    private void OpenPlayerHistoryClick(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow?.ViewModel == null) return;

        _mainWindow.ViewModel.MainView = _mainWindow.ViewModel.PlayHistoryView;
    }

    private void ReportBug_OnClick(object? sender, RoutedEventArgs e) => GeneralExtensions.OpenUrl("https://github.com/Christopher-Hayes/osuplayer/issues/new/choose");

    private void JoinDiscord_OnClick(object? sender, RoutedEventArgs e) => GeneralExtensions.OpenUrl("https://discord.gg/RJQSc5B");

    private void ContactUs_OnClick(object? sender, RoutedEventArgs e) => GeneralExtensions.OpenUrl("https://github.com/Christopher-Hayes/osuplayer#-contact");

    private void Github_OnClick(object? sender, RoutedEventArgs e) => GeneralExtensions.OpenUrl("https://github.com/Christopher-Hayes/osuplayer");
}