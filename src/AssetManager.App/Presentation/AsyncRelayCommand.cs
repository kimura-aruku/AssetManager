using System.Windows.Input;

namespace AssetManager.App.Presentation;

public sealed class AsyncRelayCommand(
    Func<Task> execute,
    Func<bool>? canExecute = null) : ICommand
{
    private bool _isRunning;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return !_isRunning && (canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isRunning = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await execute();
        }
        finally
        {
            _isRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
