using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using DynamicData;
using Nein.Base;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.Data.OsuPlayer.Classes;
using OsuPlayer.Data.OsuPlayer.StorageModels;
using OsuPlayer.IO.Storage.Blacklist;
using OsuPlayer.IO.Storage.Playlists;
using OsuPlayer.Interfaces.Service;
using OsuPlayer.Modules.Audio.Interfaces;
using ReactiveUI;
using Splat;

namespace OsuPlayer.Views;

public class SearchViewModel : BaseViewModel
{
    private readonly ReadOnlyObservableCollection<IMapEntryBase>? _filteredSongEntries;
    public readonly IPlayer Player;
    private string _filterText = string.Empty;
    private List<AddToPlaylistContextMenuEntry>? _playlistContextMenuEntries;
    private List<Playlist>? _playlists;
    private IMapEntryBase? _selectedSong;

    public string FilterText
    {
        get => _filterText;
        set => this.RaiseAndSetIfChanged(ref _filterText, value);
    }

    public ReadOnlyObservableCollection<IMapEntryBase>? FilteredSongEntries => _filteredSongEntries;

    public IMapEntryBase? SelectedSong
    {
        get => _selectedSong;
        set => this.RaiseAndSetIfChanged(ref _selectedSong, value);
    }

    public List<AddToPlaylistContextMenuEntry>? PlaylistContextMenuEntries
    {
        get => _playlistContextMenuEntries;
        set => this.RaiseAndSetIfChanged(ref _playlistContextMenuEntries, value);
    }

    public SearchViewModel(IPlayer player)
    {
        Player = player;

        var filter = this.WhenAnyValue(x => x.FilterText)
            .Throttle(TimeSpan.FromMilliseconds(20))
            .Select(BuildFilter);

        var blacklistFilter = Observable
            .FromEvent<System.ComponentModel.PropertyChangedEventHandler, System.ComponentModel.PropertyChangedEventArgs>(
                action => (_, args) => action(args),
                h => player.BlacklistChanged += h,
                h => player.BlacklistChanged -= h)
            .Select(_ => BuildBlacklistFilter())
            .StartWith(BuildBlacklistFilter());

        var sortProvider = Locator.Current.GetService<ISortProvider>();
        var filtered = player.SongSourceProvider.Songs?.Filter(blacklistFilter);

        if (sortProvider != null)
            filtered = filtered?.Sort(sortProvider.SortingModeObservable);

        filtered?
            .Filter(filter, ListFilterPolicy.ClearAndReplace)
            .ObserveOn(AvaloniaScheduler.Instance)
            .Bind(out _filteredSongEntries)
            .Subscribe();

        this.RaisePropertyChanged(nameof(FilteredSongEntries));

        Activator = new ViewModelActivator();

        this.WhenActivated(Block);
    }

    private async void Block(CompositeDisposable disposables)
    {
        Disposable.Create(() => { SelectedSong = null; }).DisposeWith(disposables);

        _playlists = (await PlaylistManager.GetAllPlaylistsAsync())?.ToList();
        PlaylistContextMenuEntries = _playlists?.Select(x => new AddToPlaylistContextMenuEntry(x.Name, AddToPlaylist)).ToList();
    }

    private async void AddToPlaylist(string name)
    {
        var playlist = _playlists?.FirstOrDefault(x => x.Name == name);

        if (playlist == null || SelectedSong == null) return;

        await PlaylistManager.AddSongToPlaylistAsync(playlist, SelectedSong);

        Player.TriggerPlaylistChanged(new PropertyChangedEventArgs(name));
    }

    private static Func<IMapEntryBase, bool> BuildBlacklistFilter()
    {
        var blacklist = new Blacklist();
        return song => !blacklist.Contains(song);
    }

    /// <summary>
    /// Builds the filter to search songs from the song's <see cref="SourceList{T}" />
    /// </summary>
    /// <param name="searchText">the search text to search songs for</param>
    /// <returns>a function with input <see cref="IMapEntryBase" /> and output <see cref="bool" /> to select found songs</returns>
    private Func<IMapEntryBase, bool> BuildFilter(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return _ => true;

        var searchQs = searchText.Split(' ');

        return song =>
        {
            return searchQs.All(x =>
                song.Title.Contains(x, StringComparison.OrdinalIgnoreCase) ||
                song.Artist.Contains(x, StringComparison.OrdinalIgnoreCase));
        };
    }
}