using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Material.Icons.Avalonia;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.Data.OsuPlayer.Enums;
using OsuPlayer.Data.OsuPlayer.StorageModels;
using OsuPlayer.IO.Storage.Playlists;
using OsuPlayer.Modules;
using OsuPlayer.Modules.Audio.Interfaces;
using OsuPlayer.UI_Extensions;
using OsuPlayer.Windows;
using Splat;
using TappedEventArgs = Avalonia.Input.TappedEventArgs;

namespace OsuPlayer.Views;

public partial class PlaylistView : ReactiveControl<PlaylistViewModel>
{
    private FluentAppWindow _mainWindow;
    private NowPlayingHighlighter? _highlighter;

    public PlaylistView()
    {
        _mainWindow = Locator.Current.GetRequiredService<FluentAppWindow>();

        InitializeComponent();

        var player = Locator.Current.GetRequiredService<IPlayer>();
        _highlighter = new NowPlayingHighlighter(SongListBox, player);
    }

    private void OpenPlaylistEditor_OnClick(object? sender, RoutedEventArgs e)
    {
        _mainWindow.ViewModel!.MainView = _mainWindow.ViewModel.PlaylistEditorView;
    }

    private void OpenBlacklistEditor_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow.ViewModel == default) return;

        _mainWindow.ViewModel.MainView = _mainWindow.ViewModel.BlacklistEditorView;
    }

    private async void PlaySong(object? sender, TappedEventArgs e)
    {
        if (sender is not Control {DataContext: IMapEntryBase song}) return;

        // Playing a song from within the playlist view activates that playlist as the playback context
        ViewModel.Player.SelectedPlaylist.Value = ViewModel.SelectedPlaylist;
        ViewModel.Player.ActivePlaylistContext.Value = ViewModel.SelectedPlaylist;

        await ViewModel.Player.TryPlaySongAsync(song);
    }

    private async void PlayPlaylist_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control {DataContext: Playlist playlist}) return;

        if (!playlist.Songs.Any())
        {
            MessageBox.Show("This playlist is empty!");

            return;
        }

        ViewModel.Player.SelectedPlaylist.Value = playlist;
        ViewModel.Player.ActivePlaylistContext.Value = playlist;

        // Always start playing a song from the selected playlist. Setting ActivePlaylistContext
        // alone only auto-advances when the current song isn't in the playlist; if it already
        // is in the playlist the context label updates but playback never changes.
        await ViewModel.Player.NextSong(PlayDirection.Forward);
    }

    private void PlaylistItemLoaded(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Grid grid) return;

        var icon = (MaterialIcon) grid.Children.FirstOrDefault(x => x is MaterialIcon);

        if (icon == null) return;

        ViewModel.AddIcon(icon);
    }

    private async void MenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPlaylist == null || ViewModel.SelectedSong == null) return;

        await PlaylistManager.RemoveSongFromPlaylist(ViewModel.SelectedPlaylist, ViewModel.SelectedSong);

        ViewModel.Player.TriggerPlaylistChanged(new PropertyChangedEventArgs(ViewModel.SelectedPlaylist.Name));
    }
}