using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System.Reactive.Linq;

namespace Nein.Base;

/// <summary>
/// A reactive window class for all windows in our app
/// </summary>
/// <typeparam name="TViewModel">the ViewModel which the <see cref="ReactiveWindow{TViewModel}" /> is bound to</typeparam>
public class ReactiveWindow<TViewModel> : Window, IViewFor<TViewModel> where TViewModel : ReactiveObject
{
    public static readonly StyledProperty<TViewModel> ViewModelProperty =
        AvaloniaProperty
            .Register<ReactiveWindow<TViewModel>, TViewModel>(nameof(ViewModel));

    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (TViewModel) value;
    }

    public TViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public ReactiveWindow()
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
            ViewModel = null;
    }

    private void OnViewModelChanged(TViewModel? value)
    {
        if (value == null)
            ClearValue(DataContextProperty);
        else if (DataContext != value)
            DataContext = value;
    }
}
