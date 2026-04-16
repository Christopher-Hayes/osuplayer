using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Nein.Base;
using Nein.Extensions;
using OsuPlayer.Windows;
using Splat;

namespace OsuPlayer.Views;

internal partial class TopBarView : ReactiveControl<TopBarViewModel>
{
    private FluentAppWindow? _mainWindow;

    public TopBarView()
    {
        InitializeComponent();

        _mainWindow = Locator.Current.GetRequiredService<FluentAppWindow>();
    }
    private void Navigation_Clicked(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow?.ViewModel == default) return;

        switch ((sender as Control)?.Name)
        {
            case "SearchNavigation":
                _mainWindow.ViewModel!.MainView = _mainWindow.ViewModel.SearchView;
                break;
            case "ArtistsNavigation":
                _mainWindow.ViewModel!.MainView = _mainWindow.ViewModel.ArtistsView;
                break;
            case "PlaylistNavigation":
                _mainWindow.ViewModel!.MainView = _mainWindow.ViewModel.PlaylistView;
                break;
            case "HomeNavigation":
                _mainWindow.ViewModel!.MainView = _mainWindow.ViewModel.HomeView;
                break;
        }
    }

    private void TopBarGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_mainWindow == default) return;

        _mainWindow.BeginMoveDrag(e);
        e.Handled = false;
    }
}