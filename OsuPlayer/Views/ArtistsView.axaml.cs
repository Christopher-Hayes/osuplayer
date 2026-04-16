using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Windows;
using Splat;

namespace OsuPlayer.Views;

public partial class ArtistsView : ReactiveControl<ArtistsViewModel>
{
    private FluentAppWindow? _mainWindow;

    public ArtistsView()
    {
        InitializeComponent();

        _mainWindow = Locator.Current.GetRequiredService<FluentAppWindow>();
    }

    private void FilterText_Changed(object? sender, TextChangedEventArgs e)
    {
        ViewModel?.ApplyFilter();
    }

    private void ArtistListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_mainWindow?.ViewModel == null) return;
        if (ViewModel?.SelectedArtist == null) return;

        var artistView = _mainWindow.ViewModel.ArtistView;
        _ = artistView.LoadArtistAsync(ViewModel.SelectedArtist.Name);
        _mainWindow.ViewModel.MainView = artistView;
    }
}
