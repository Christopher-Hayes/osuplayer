using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Modules.Audio.Interfaces;
using ReactiveUI;
using Splat;

namespace OsuPlayer.Views.CustomControls;

public partial class AudioVisualizerView : ReactiveUserControl<AudioVisualizerViewModel>
{
    private object _lockObj = new ();

    public AudioVisualizerView()
    {
        InitializeComponent();

        this.WhenActivated(_ =>
        {
            ViewModel.AudioVisualizerUpdateTimer.Interval = TimeSpan.FromMilliseconds(2);
            ViewModel.AudioVisualizerUpdateTimer.Tick += AudioVisualizerUpdateTimer_OnTick;

            ViewModel.AudioVisualizerUpdateTimer.Start();
        });

        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (ViewModel == default) return;

        ViewModel.UpdateBarCount(e.NewSize.Width);
    }

    private void AudioVisualizerUpdateTimer_OnTick(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            using var config = new Config();

            // Do nothing if audio visualizer is disabled
            if (!config.Container.DisplayAudioVisualizer) return;

            var player = Locator.Current.GetRequiredService<IPlayer>();

            if (ViewModel == default) return;

            if (!player.IsPlaying.Value)
            {
                foreach (var t in ViewModel.SeriesValues.Where(x => x.Value != 0))
                {
                    t.Value = 0;
                }

                return;
            }

            lock (_lockObj)
            {
                // var audioEngine = Locator.Current.GetRequiredService<IAudioEngine>();

                var vData = ViewModel.AudioEngine.GetVisualizationData();
                var barCount = ViewModel.SeriesValues.Count;
                var step = vData.Length / (double)barCount;

                for (var i = 0; i < barCount; i++)
                {
                    // Average the FFT bins that map to this bar
                    var startBin = (int)(i * step);
                    var endBin = Math.Min((int)((i + 1) * step), vData.Length);
                    var sum = 0.0;
                    for (var b = startBin; b < endBin; b++)
                        sum += vData[b];
                    var avg = endBin > startBin ? sum / (endBin - startBin) : 0.0;

                    // square root scaling for better visual distribution, clamped to Y-axis MaxLimit
                    var scaled = Math.Min(Math.Pow(avg, 0.6) * 3, 1.0);
                    ViewModel.SeriesValues[i].Value = scaled < 0.01 ? 0 : scaled;
                }
            }
        });
    }
}