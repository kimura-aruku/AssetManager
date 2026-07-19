using System.Windows.Input;

namespace AssetManager.App.Presentation;

public sealed class RelayCommand(
    Action execute,
    Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        execute();
    }

    public static void RefreshCanExecute()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

public sealed class RelayCommand<T>(
    Action<T> execute,
    Predicate<T>? canExecute = null) : ICommand
    where T : class
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return parameter is T value && (canExecute?.Invoke(value) ?? true);
    }

    public void Execute(object? parameter)
    {
        if (parameter is T value)
        {
            execute(value);
        }
    }
}
