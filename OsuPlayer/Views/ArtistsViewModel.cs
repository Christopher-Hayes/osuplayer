using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
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
    private ReadOnlyObservableCollection<IList<ArtistEntry>>? _artistRows;
    private string _filterText = string.Empty;
    private int _columnCount = 4;

    /// <summary>Flat list, kept for filtering logic.</summary>
    public ReadOnlyObservableCollection<ArtistEntry>? Artists => _artists;

    /// <summary>Artists grouped into rows for virtualizing ListBox rendering.</summary>
    public ReadOnlyObservableCollection<IList<ArtistEntry>>? ArtistRows => _artistRows;

    public string FilterText
    {
        get => _filterText;
        set => this.RaiseAndSetIfChanged(ref _filterText, value);
    }

    /// <summary>
    /// Number of card columns. Set from code-behind based on the scroll viewer width.
    /// Triggers a re-grouping of <see cref="ArtistRows"/>.
    /// </summary>
    public int ColumnCount
    {
        get => _columnCount;
        set
        {
            if (_columnCount == value) return;
            this.RaiseAndSetIfChanged(ref _columnCount, value);
            RebuildRows();
        }
    }

    /// <summary>
    /// Pixel width of each card, derived from <see cref="ColumnCount"/> and the available width.
    /// Updated by code-behind alongside <see cref="ColumnCount"/>.
    /// </summary>
    public double CardWidth
    {
        get => _cardWidth;
        set => this.RaiseAndSetIfChanged(ref _cardWidth, value);
    }

    private double _cardWidth = 180;

    /// <summary>
    /// Saved scroll offset, persisted across view re-creations so navigating back
    /// restores the previous scroll position.
    /// </summary>
    public Vector SavedScrollOffset { get; set; }

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
            .Select(g =>
            {
                var first = g.First();
                return new ArtistEntry(g.Key, g.Count(), first, GetCachedImagePathIfExists(g.Key));
            })
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filtered = string.IsNullOrWhiteSpace(_filterText)
            ? grouped
            : grouped.Where(a => a.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)).ToList();

        _artists = new ReadOnlyObservableCollection<ArtistEntry>(new ObservableCollection<ArtistEntry>(filtered));
        this.RaisePropertyChanged(nameof(Artists));
        RebuildRows();
    }

    private void RebuildRows()
    {
        if (_artists == null) return;

        var cols = Math.Max(1, _columnCount);
        var rows = _artists
            .Select((entry, i) => (entry, i))
            .GroupBy(t => t.i / cols)
            .Select(g => (IList<ArtistEntry>)g.Select(t => t.entry).ToList())
            .ToList();

        _artistRows = new ReadOnlyObservableCollection<IList<ArtistEntry>>(
            new ObservableCollection<IList<ArtistEntry>>(rows));
        this.RaisePropertyChanged(nameof(ArtistRows));
    }

    /// <summary>
    /// Returns the local cached artist image path if it exists on disk, otherwise null.
    /// Uses the same MD5-based filename scheme as <see cref="ArtistViewModel"/>.
    /// Does not make any network calls.
    /// </summary>
    private static string? GetCachedImagePathIfExists(string artistName)
    {
        try
        {
            using var md5 = MD5.Create();
            var hash = BitConverter
                .ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(artistName.ToLowerInvariant())))
                .Replace("-", "").ToLower();
            var path = Path.Combine("data", "artist_cache", $"{hash}_image.png");
            return File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    // Called when the filter text changes
    public void ApplyFilter()
    {
        RefreshArtists();
    }
}

public record ArtistEntry(string Name, int SongCount, IMapEntryBase? FirstSong = null, string? CachedImagePath = null)
{
    public bool ShowSongCount => SongCount > 1;
}
