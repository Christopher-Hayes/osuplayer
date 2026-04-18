using Avalonia;
using FluentAvalonia.UI.Windowing;
using ReactiveUI;

namespace Nein.Base;

public class FluentReactiveWindow<TViewModel> : FAAppWindow, IViewFor<TViewModel>, IViewFor, IActivatableView
    where TViewModel : ReactiveObject
{
    public static readonly StyledProperty<TViewModel> ViewModelProperty =
        AvaloniaProperty.Register<FluentReactiveWindow<TViewModel>, TViewModel>(nameof(ViewModel));

    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (TViewModel) value;
    }

    public TViewModel? ViewModel
    {
        get => this.GetValue(ViewModelProperty);
        set => this.SetValue(ViewModelProperty, value);
    }

    public FluentReactiveWindow()
    {
        this.WhenActivated(disposables => { });
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataContextProperty)
            OnDataContextChanged(change.NewValue);
        else if (change.Property == ViewModelProperty)
            OnViewModelChanged(change.NewValue as TViewModel);
    }

    private void OnDataContextChanged(object? value)
    {
        if (value is TViewModel viewModel)
            ViewModel = viewModel;
        else
            ViewModel = default;
    }

    private void OnViewModelChanged(TViewModel? value)
    {
        if (value == null)
        {
            this.ClearValue(DataContextProperty);
        }
        else
        {
            if (DataContext == value)
                return;
            DataContext = value;
        }
    }
}
