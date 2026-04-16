using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.IO.Storage.Blacklist;
using OsuPlayer.Modules;
using OsuPlayer.Modules.Audio.Interfaces;
using Splat;

namespace OsuPlayer.Views;

public partial class SearchView : ReactiveControl<SearchViewModel>
{
    private NowPlayingHighlighter? _highlighter;

    public SearchView()
    {
        InitializeComponent();

        var player = Locator.Current.GetRequiredService<IPlayer>();
        _highlighter = new NowPlayingHighlighter(SongListBox, player);
    }

    private async void InputElement_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        var list = sender as ListBox;
        var song = list!.SelectedItem as IMapEntryBase;

        // Playing from search clears any active playlist context
        ViewModel.Player.ActivePlaylistContext.Value = null;
        ViewModel.Player.ActiveArtistContext.Value = null;

        await ViewModel.Player.TryPlaySongAsync(song);
    }

    private void AddToBlacklist_OnClick(object? sender, RoutedEventArgs e)
    {
        using var blacklist = new Blacklist();

        blacklist.Container.Songs.Add(ViewModel.SelectedSong?.Hash);
    }
}