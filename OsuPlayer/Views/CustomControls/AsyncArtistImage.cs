using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using OsuPlayer.Data.DataModels;
using OsuPlayer.Data.DataModels.Interfaces;

namespace OsuPlayer.Views.CustomControls;

/// <summary>
/// Async image panel for artist grid cards.
/// Loads (in priority order) a cached artist image from a local file path, or
/// falls back to the first song's background cover — both entirely from disk
/// with no network calls. Debounced to avoid saturating I/O when scrolling.
/// </summary>
public class AsyncArtistImage : Panel
{
    public static readonly StyledProperty<string?> CachedImagePathProperty =
        AvaloniaProperty.Register<AsyncArtistImage, string?>(nameof(CachedImagePath));

    public static readonly StyledProperty<IMapEntryBase?> FirstSongProperty =
        AvaloniaProperty.Register<AsyncArtistImage, IMapEntryBase?>(nameof(FirstSong));

    public string? CachedImagePath
    {
        get => GetValue(CachedImagePathProperty);
        set => SetValue(CachedImagePathProperty, value);
    }

    public IMapEntryBase? FirstSong
    {
        get => GetValue(FirstSongProperty);
        set => SetValue(FirstSongProperty, value);
    }

    static AsyncArtistImage()
    {
        CachedImagePathProperty.Changed.AddClassHandler<AsyncArtistImage>((c, _) => c.Reload());
        FirstSongProperty.Changed.AddClassHandler<AsyncArtistImage>((c, _) => c.Reload());
    }

    private CancellationTokenSource? _cts;
    private Bitmap? _bitmap;
    private readonly Image _image;

    private const int DebounceMs = 100;

    /// <summary>Max pixels on the longest side when decoding — keeps memory and decode time low.</summary>
    private const int DecodeSize = 200;

    /// <summary>Limits how many images decode concurrently to avoid saturating I/O and CPU.</summary>
    private static readonly SemaphoreSlim DecodeSemaphore = new(4);

    private static readonly Animation FadeIn = new()
    {
        Duration = TimeSpan.FromMilliseconds(200),
        Easing = new CubicEaseOut(),
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, 0d) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, 1d) } }
        }
    };

    public AsyncArtistImage()
    {
        _image = new Image
        {
            Stretch = Stretch.UniformToFill,
            Opacity = 0,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };
        Children.Add(_image);
    }

    private void Reload()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _image.Opacity = 0;
        _image.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;

        _ = LoadAsync(_cts.Token);
    }

    private async Task LoadAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(DebounceMs, token);

            // Prefer the cached artist image; fall back to first song's background
            var path = CachedImagePath;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                path = ResolveSongBackgroundPath(FirstSong);

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            await DecodeSemaphore.WaitAsync(token);
            Bitmap? bitmap;
            try
            {
                bitmap = await Task.Run(() =>
                {
                    try
                    {
                        using var stream = File.OpenRead(path);
                        return Bitmap.DecodeToWidth(stream, DecodeSize, BitmapInterpolationMode.MediumQuality);
                    }
                    catch { return null; }
                }, token);
            }
            finally
            {
                DecodeSemaphore.Release();
            }

            if (token.IsCancellationRequested)
            {
                bitmap?.Dispose();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested)
                {
                    bitmap?.Dispose();
                    return;
                }
                _bitmap?.Dispose();
                _bitmap = bitmap;
                _image.Source = bitmap;
                _ = FadeIn.RunAsync(_image);
            });
        }
        catch (OperationCanceledException) { }
    }

    private static string? ResolveSongBackgroundPath(IMapEntryBase? song)
    {
        if (song is RealmMapEntryBase realm && !string.IsNullOrEmpty(realm.BackgroundFileLocation)
            && File.Exists(realm.BackgroundFileLocation))
            return realm.BackgroundFileLocation;

        return null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _bitmap?.Dispose();
        _bitmap = null;
        _image.Source = null;
    }
}
