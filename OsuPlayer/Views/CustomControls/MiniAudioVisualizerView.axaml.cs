using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Nein.Extensions;
using OsuPlayer.Data.DataModels.Interfaces;
using OsuPlayer.Modules.Audio.Interfaces;
using Splat;

namespace OsuPlayer.Views.CustomControls;

/// <summary>
/// A compact 5-bar audio visualizer intended to be overlaid on song cover art in a list.
/// It is only visible when its bound <see cref="Entry"/> matches the currently playing song.
/// </summary>
public partial class MiniAudioVisualizerView : UserControl
{
    public static readonly StyledProperty<IMapEntryBase?> EntryProperty =
        AvaloniaProperty.Register<MiniAudioVisualizerView, IMapEntryBase?>(nameof(Entry));

    public IMapEntryBase? Entry
    {
        get => GetValue(EntryProperty);
        set => SetValue(EntryProperty, value);
    }

    private readonly IPlayer _player;
    private readonly IAudioEngine _audioEngine;
    private readonly DispatcherTimer _timer;
    private readonly Border[] _bars;

    private const int BarCount = 5;
    private const double CanvasHeight = 44.0; // matches the Canvas Height in AXAML
    private const double MinBarHeight = 5.0;  // same as width → circle when silent
    private const double MaxBarHeight = 38.0; // bars extend up+down from center

    // How quickly bars chase their target height (0 = never moves, 1 = instant snap).
    // A value around 0.15–0.25 gives a smooth, non-jumpy feel at ~30 fps.
    private const double Smoothing = 0.6;

    // Only run the FFT every Nth render tick. The lerp fills in the frames between
    // samples, so the animation stays smooth while the DSP load drops proportionally.
    private const int FftSampleInterval = 3; // FFT at ~10 Hz, render at ~30 fps
    private int _ticksSinceLastSample;

    private readonly double[] _currentHeights;
    private readonly double[] _targetHeights;

    public MiniAudioVisualizerView()
    {
        InitializeComponent();

        _player = Locator.Current.GetRequiredService<IPlayer>();
        _audioEngine = Locator.Current.GetRequiredService<IAudioEngine>();

        _bars = [Bar1, Bar2, Bar3, Bar4, Bar5];
        _currentHeights = Enumerable.Repeat(MinBarHeight, BarCount).ToArray();
        _targetHeights  = Enumerable.Repeat(MinBarHeight, BarCount).ToArray();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // ~30 fps
        _timer.Tick += OnTimerTick;
        // Timer is started/stopped in OnAttachedToVisualTree / OnDetachedFromVisualTree
        // so off-screen (virtualized-out) instances don't burn CPU.
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateVisibility()
    {
        IsVisible = Entry != null
                    && _player.IsPlaying.Value
                    && _player.CurrentSong.Value?.Hash == Entry.Hash;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        // Re-evaluate visibility every tick — cheap hash comparison, avoids GC'd subscription bugs
        UpdateVisibility();

        if (!IsVisible) return;

        // Only re-sample the FFT every FftSampleInterval ticks to reduce DSP cost.
        // The lerp below keeps the animation smooth between samples.
        if (++_ticksSinceLastSample >= FftSampleInterval)
        {
            _ticksSinceLastSample = 0;

            var vData = _audioEngine.GetVisualizationData();
            if (vData.Length == 0) return;

            // Focus on the lower 20% of frequencies, which tend to be more visually interesting and less noisy than the top end.
            var usableBins = (int)(vData.Length * 0.2);
            var step = usableBins / (double)BarCount;

            for (var i = 0; i < BarCount; i++)
            {
                var startBin = (int)(i * step);
                var endBin = Math.Min((int)((i + 1) * step), usableBins);

                var sum = 0.0;
                for (var b = startBin; b < endBin; b++)
                    sum += vData[b];

                var avg = endBin > startBin ? sum / (endBin - startBin) : 0.0;
                var scaled = Math.Min(Math.Pow(avg, 0.75) * 8.0, 1.0);

                _targetHeights[i] = MinBarHeight + scaled * (MaxBarHeight - MinBarHeight);
            }
        }

        // Lerp current heights toward targets every render tick for smooth motion.
        ReadOnlySpan<int> barOrder = [2, 3, 1, 4, 0]; // center bar updates first for better visual impact
        for (var i = 0; i < BarCount; i++)
        {
            _currentHeights[i] += (_targetHeights[i] - _currentHeights[i]) * Smoothing;
            var h = _currentHeights[i];
            _bars[barOrder[i]].Height = h;
            // Pin the vertical center to the canvas midpoint so the bar grows up and down equally.
            Canvas.SetTop(_bars[barOrder[i]], (CanvasHeight - h) / 2.0);
        }
    }
}
