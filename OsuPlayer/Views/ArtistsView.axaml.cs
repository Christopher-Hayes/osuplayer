using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Windows;
using Splat;

namespace OsuPlayer.Views;

public partial class ArtistsView : ReactiveControl<ArtistsViewModel>
{
    private FluentAppWindow? _mainWindow;

    // Card margin is 4px left + 4px right = 8px per card.
    private const double CardMargin = 8;
    private const double MinCardWidth = 110;

    public ArtistsView()
    {
        InitializeComponent();
        _mainWindow = Locator.Current.GetRequiredService<FluentAppWindow>();

        // Restore the saved scroll offset once the ListBox has been laid out.
        ArtistRowListBox.LayoutUpdated += OnFirstLayoutUpdated;
    }

    private void OnFirstLayoutUpdated(object? sender, EventArgs e)
    {
        // Only need to fire once per view creation.
        ArtistRowListBox.LayoutUpdated -= OnFirstLayoutUpdated;

        if (ViewModel == null) return;
        var saved = ViewModel.SavedScrollOffset;
        if (saved == default) return;

        // Post at Background so virtualising panel has finished measuring.
        Dispatcher.UIThread.Post(() =>
        {
            var sv = GetScrollViewer();
            if (sv != null) sv.Offset = saved;
        }, DispatcherPriority.Background);
    }

    private ScrollViewer? GetScrollViewer() =>
        ArtistRowListBox.FindDescendantOfType<ScrollViewer>();

    private void FilterText_Changed(object? sender, TextChangedEventArgs e)
    {
        ViewModel?.ApplyFilter();
    }

    private void ArtistCard_Tapped(object? sender, TappedEventArgs e)
    {
        if (_mainWindow?.ViewModel == null) return;
        if (sender is not Control control || control.DataContext is not ArtistEntry entry) return;

        // Save the current scroll position to the ViewModel (which survives view re-creation).
        if (ViewModel != null)
            ViewModel.SavedScrollOffset = GetScrollViewer()?.Offset ?? default;

        var artistView = _mainWindow.ViewModel.ArtistView;
        _ = artistView.LoadArtistAsync(entry.Name);
        _mainWindow.ViewModel.MainView = artistView;
    }

    private void ArtistRowListBox_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (ViewModel == null) return;
        var availableWidth = e.NewSize.Width - 8; // subtract ListBox padding (4 each side)
        if (availableWidth <= 0) return;

        // Compute how many columns fit, targeting ~190px per card (180 + 8 margin + small buffer)
        var cols = Math.Max(1, (int)(availableWidth / (180 + CardMargin + 2)));
        var cardWidth = Math.Max(MinCardWidth, (availableWidth / cols) - CardMargin);

        ViewModel.CardWidth = cardWidth;
        ViewModel.ColumnCount = cols;
    }
}
