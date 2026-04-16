using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using DynamicData;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.Modules.Audio.Interfaces;
using ReactiveUI;
using Splat;

namespace OsuPlayer.Views;

public class ArtistsViewModel : BaseViewModel
{
    public readonly IPlayer Player;

    private ReadOnlyObservableCollection<ArtistEntry>? _artists;
    private string _filterText = string.Empty;
    private ArtistEntry? _selectedArtist;

    public ReadOnlyObservableCollection<ArtistEntry>? Artists => _artists;

    public string FilterText
    {
        get => _filterText;
        set => this.RaiseAndSetIfChanged(ref _filterText, value);
    }

    public ArtistEntry? SelectedArtist
    {
        get => _selectedArtist;
        set => this.RaiseAndSetIfChanged(ref _selectedArtist, value);
    }

    public ArtistsViewModel(IPlayer player)
    {
        Player = player;

        Activator = new ViewModelActivator();

        this.WhenActivated(disposables =>
        {
            Disposable.Create(() => { }).DisposeWith(disposables);

            RefreshArtists();
        });

        // Re-build artist list when the song source changes
        Player.SongSourceProvider.Songs?.Subscribe(_ => RefreshArtists());
    }

    // Parameterless constructor for the designer
    public ArtistsViewModel() : this(Locator.Current.GetRequiredService<IPlayer>())
    {
    }

    private void RefreshArtists()
    {
        var songs = Player.SongSourceProvider.SongSourceList;
        if (songs == null) return;

        var grouped = songs
            .GroupBy(s => s.Artist, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ArtistEntry(g.Key, g.Count()))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filtered = string.IsNullOrWhiteSpace(_filterText)
            ? grouped
            : grouped.Where(a => a.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)).ToList();

        _artists = new ReadOnlyObservableCollection<ArtistEntry>(new ObservableCollection<ArtistEntry>(filtered));
        this.RaisePropertyChanged(nameof(Artists));
    }

    // Called when the filter text changes
    public void ApplyFilter()
    {
        RefreshArtists();
    }
}

public record ArtistEntry(string Name, int SongCount);
