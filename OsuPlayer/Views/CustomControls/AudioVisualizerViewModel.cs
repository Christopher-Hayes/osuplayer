using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Modules.Audio.Interfaces;
using ReactiveUI;
using SkiaSharp;

namespace OsuPlayer.Views.CustomControls;

public class AudioVisualizerViewModel : BaseViewModel
{
    public IAudioEngine AudioEngine { get; set; }

    private DispatcherTimer _audioVisualizerUpdateTimer = new();

    public DispatcherTimer AudioVisualizerUpdateTimer
    {
        get => _audioVisualizerUpdateTimer;
        set => this.RaiseAndSetIfChanged(ref _audioVisualizerUpdateTimer, value);
    }

    private ObservableCollection<ObservableValue> _seriesValues = new();

    public ObservableCollection<ObservableValue> SeriesValues
    {
        get => _seriesValues;
        set => this.RaiseAndSetIfChanged(ref _seriesValues, value);
    }

    private ISeries[] _series = Array.Empty<ISeries>();

    public ISeries[] Series
    {
        get => _series;
        set => this.RaiseAndSetIfChanged(ref _series, value);
    }

    public Axis[] XAxes { get; set; } =
    {
        new Axis
        {
            SeparatorsPaint = null,
            LabelsPaint = null,
            TicksPaint = null,
            SubticksPaint = null,
            ZeroPaint = null,
            ShowSeparatorLines = false,
            MaxLimit = 256
        }
    };

    public Axis[] YAxes { get; set; } =
    {
        new Axis
        {
            MinLimit = 0,
            MaxLimit = 1,
            SeparatorsPaint = null,
            LabelsPaint = null,
            TicksPaint = null,
            SubticksPaint = null,
            ShowSeparatorLines = false,
        }
    };

    public AudioVisualizerViewModel(IAudioEngine audioEngine)
    {
        AudioEngine = audioEngine;

        const int size = 2048;

        SeriesValues = new ObservableValue[size].ToObservableCollection();

        for (var i = 0; i < size; i++)
        {
            SeriesValues[i] = new ObservableValue(0);
        }

        Series = new ISeries[]
        {
            new ColumnSeries<ObservableValue>
            {
                Values = SeriesValues,
                IsHoverable = false,
                Fill = new SolidColorPaint(new SKColor(164, 164, 164, 30)),
                Stroke = null
            }
        };

        Activator = new ViewModelActivator();
    }

    /// <summary>
    /// Resizes the bar count to match the available pixel width.
    /// One bar per ~2 pixels, clamped to [64, 2048].
    /// </summary>
    public void UpdateBarCount(double pixelWidth)
    {
        var desired = Math.Clamp((int)(pixelWidth / 7.0), 64, 2048);

        if (desired == SeriesValues.Count) return;

        // Resize in bulk without triggering per-item notifications
        if (desired > SeriesValues.Count)
        {
            while (SeriesValues.Count < desired)
                SeriesValues.Add(new ObservableValue(0));
        }
        else
        {
            while (SeriesValues.Count > desired)
                SeriesValues.RemoveAt(SeriesValues.Count - 1);
        }

        // Keep the X-axis limit in sync
        XAxes[0].MaxLimit = desired;
    }
}