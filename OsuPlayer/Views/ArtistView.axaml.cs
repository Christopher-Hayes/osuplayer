using Avalonia.Controls;
using Avalonia.Interactivity;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.Modules;
using OsuPlayer.Modules.Audio.Interfaces;
using OsuPlayer.Windows;
using Splat;
using TappedEventArgs = Avalonia.Input.TappedEventArgs;

namespace OsuPlayer.Views;

public partial class ArtistView : ReactiveControl<ArtistViewModel>
{
    private FluentAppWindow? _mainWindow;
    private NowPlayingHighlighter? _highlighter;

    public ArtistView()
    {
        InitializeComponent();

        _mainWindow = Locator.Current.GetRequiredService<FluentAppWindow>();

        var player = Locator.Current.GetRequiredService<IPlayer>();
        _highlighter = new NowPlayingHighlighter(SongListBox, player);
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow?.ViewModel == null) return;

        _mainWindow.ViewModel.MainView = _mainWindow.ViewModel.ArtistsView;
    }

    private async void SongItem_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: IMapEntryBase song }) return;

        // Set artist context so next/prev only navigates within this artist's songs
        ViewModel.Player.ActivePlaylistContext.Value = null;
        ViewModel.Player.ActiveArtistContext.Value = ViewModel.ArtistName;

        await ViewModel.Player.TryPlaySongAsync(song);
    }

    private void SimilarArtist_Click(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow?.ViewModel == null) return;
        if (sender is not Control { DataContext: SimilarArtistDisplayEntry artist }) return;

        // Navigate to that artist's page
        var artistView = _mainWindow.ViewModel.ArtistView;
        _ = artistView.LoadArtistAsync(artist.Name);
        _mainWindow.ViewModel.MainView = artistView;
    }
}
