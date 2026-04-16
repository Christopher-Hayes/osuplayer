using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using DynamicData;
using Nein.Base;
using OsuPlayer.Data.DataModels;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.Data.OsuPlayer.Classes;
using OsuPlayer.Data.OsuPlayer.Enums;
using OsuPlayer.Data.OsuPlayer.StorageModels;
using OsuPlayer.IO.Importer;
using OsuPlayer.IO.Storage.Playlists;
using OsuPlayer.Interfaces.Service;
using OsuPlayer.Modules.Audio.Interfaces;
using ReactiveUI;
using Splat;

namespace OsuPlayer.Views;

public class HomeViewModel : BaseViewModel
{
    private readonly Bindable<bool> _songsLoading = new();
    private readonly ReadOnlyObservableCollection<IMapEntryBase>? _sortedSongEntries;
    private readonly Bindable<SortingMode> _sortingMode = new();
    private readonly ISortProvider? _sortProvider;

    public readonly IPlayer Player;

    private List<AddToPlaylistContextMenuEntry>? _playlistContextMenuEntries;
    private List<Playlist>? _playlists;
    private IMapEntryBase? _selectedSong;

    public ReadOnlyObservableCollection<IMapEntryBase>? SortedSongEntries => _sortedSongEntries;

    public IEnumerable<SortingMode> SortingModes => Enum.GetValues<SortingMode>();

    public SortingMode SelectedSortingMode
    {
        get => _sortingMode.Value;
        set
        {
            _sortingMode.Value = value;
            this.RaisePropertyChanged();

            using var config = new Config();
            config.Container.SortingMode = value;
        }
    }

    public IMapEntryBase? SelectedSong
    {
        get => _selectedSong;
        set => this.RaiseAndSetIfChanged(ref _selectedSong, value);
    }

    public bool SongsLoading => new Config().Container.OsuPath != null && _songsLoading.Value;

    public List<AddToPlaylistContextMenuEntry>? PlaylistContextMenuEntries
    {
        get => _playlistContextMenuEntries;
        set => this.RaiseAndSetIfChanged(ref _playlistContextMenuEntries, value);
    }

    public HomeViewModel(IPlayer player)
    {
        Player = player;

        _sortProvider = Locator.Current.GetService<ISortProvider>();

        if (_sortProvider != null)
        {
            _sortingMode.BindTo(_sortProvider.SortingModeBindable);
            _sortingMode.BindValueChanged(_ => this.RaisePropertyChanged(nameof(SelectedSortingMode)));
        }

        _songsLoading.BindTo(((IImportNotifications) Player).SongsLoading);
        _songsLoading.BindValueChanged(_ => this.RaisePropertyChanged(nameof(SongsLoading)));

        player.SongSourceProvider.Songs?.ObserveOn(AvaloniaScheduler.Instance).Bind(out _sortedSongEntries).Subscribe();

        this.RaisePropertyChanged(nameof(SortedSongEntries));

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
}