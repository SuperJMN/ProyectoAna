using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;

namespace EvaluacionesApp.Desktop.ViewModels;

public abstract class MenuItemViewModelBase : ReactiveObject
{
    static readonly ReadOnlyObservableCollection<MenuItemViewModel> EmptyChildren = new(new ObservableCollection<MenuItemViewModel>());

    string header = string.Empty;
    object? icon;
    ICommand? command;
    object? commandParameter;
    ReadOnlyObservableCollection<MenuItemViewModel> children = EmptyChildren;
    string key = string.Empty;

    public string Header
    {
        get => header;
        set => this.RaiseAndSetIfChanged(ref header, value);
    }

    public object? Icon
    {
        get => icon;
        set => this.RaiseAndSetIfChanged(ref icon, value);
    }

    public ICommand? Command
    {
        get => command;
        set => this.RaiseAndSetIfChanged(ref command, value);
    }

    public object? CommandParameter
    {
        get => commandParameter;
        set => this.RaiseAndSetIfChanged(ref commandParameter, value);
    }

    public ReadOnlyObservableCollection<MenuItemViewModel> Children => children;

    protected void SetChildren(ReadOnlyObservableCollection<MenuItemViewModel> newChildren)
    {
        if (!ReferenceEquals(children, newChildren))
        {
            children = newChildren;
            this.RaisePropertyChanged(nameof(Children));
        }
    }

    public string Key
    {
        get => key;
        protected set => this.RaiseAndSetIfChanged(ref key, value);
    }
}

public class MenuViewModel : MenuItemViewModelBase
{
}

public class MenuItemViewModel : MenuItemViewModelBase
{
}
