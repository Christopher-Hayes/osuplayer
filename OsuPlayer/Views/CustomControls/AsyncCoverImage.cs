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
/// A panel that asynchronously loads and displays a song cover image from the local osu! files store.
/// Debounces load requests so rapid scrolling does not saturate the thread pool with I/O.
/// </summary>
public class AsyncCoverImage : Panel
{
    public static readonly StyledProperty<IMapEntryBase?> EntryProperty =
        AvaloniaProperty.Register<AsyncCoverImage, IMapEntryBase?>(nameof(Entry));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<AsyncCoverImage, CornerRadius>(nameof(CornerRadius), new CornerRadius(6));

    public IMapEntryBase? Entry
    {
        get => GetValue(EntryProperty);
        set => SetValue(EntryProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    static AsyncCoverImage()
    {
        EntryProperty.Changed.AddClassHandler<AsyncCoverImage>((c, _) => c.OnEntryChanged());
        CornerRadiusProperty.Changed.AddClassHandler<AsyncCoverImage>((c, _) => c.UpdateCornerRadius());
    }

    private CancellationTokenSource? _cts;
    private Bitmap? _bitmap;
    private readonly Image _image;
    private readonly Border _border;

    private static readonly Animation FadeIn = new()
    {
        Duration = TimeSpan.FromMilliseconds(250),
        Easing = new CubicEaseOut(),
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, 0d) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, 1d) } }
        }
    };

    /// <summary>
    /// How long to wait after an entry is set before starting the load.
    /// Keeps scroll smooth — items that fly past quickly are never loaded.
    /// </summary>
    private const int DebounceMs = 120;

    /// <summary>
    /// Maximum pixel width to decode cover images at. Covers are displayed at small sizes
    /// (44–64 px) so decoding multi-megapixel osu! backgrounds at full resolution wastes
    /// GPU memory and hurts compositing performance during animations.
    /// We use 2× the largest display size to stay sharp on HiDPI screens.
    /// </summary>
    private const int DecodePixelWidth = 128;

    public AsyncCoverImage()
    {
        _image = new Image
        {
            Stretch = Stretch.UniformToFill,
            Opacity = 0,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        _border = new Border
        {
            CornerRadius = CornerRadius,
            ClipToBounds = true,
            Child = _image
        };

        Children.Add(_border);
    }

    private void UpdateCornerRadius()
    {
        _border.CornerRadius = CornerRadius;
    }

    private void OnEntryChanged()
    {
        // Cancel any in-flight or waiting load
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        // Clear the current image immediately so stale art doesn't linger while scrolling
        _image.Opacity = 0;
        _image.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;

        var path = (Entry as RealmMapEntryBase)?.BackgroundFileLocation;

        if (string.IsNullOrEmpty(path))
            return;

        _ = LoadAsync(path, _cts.Token);
    }

    private async Task LoadAsync(string path, CancellationToken token)
    {
        try
        {
            // Debounce: if the item is scrolled past quickly this delay is cancelled
            await Task.Delay(DebounceMs, token);

            if (!File.Exists(path))
                return;

            // Load the bitmap off the UI thread
            var bitmap = await Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    return Bitmap.DecodeToWidth(stream, DecodePixelWidth, BitmapInterpolationMode.LowQuality);
                }
                catch { return null; }
            }, token);

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
