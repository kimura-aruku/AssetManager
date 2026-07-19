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
