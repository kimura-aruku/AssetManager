using AssetManager.Application.Startup;

namespace AssetManager.App.Presentation;

public sealed class StartupWindowViewModel : ObservableObject, IProgress<StartupProgress>
{
    private string _message = "起動準備を開始しています。";
    private int _completedSteps;
    private int _totalSteps = 1;

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public int CompletedSteps
    {
        get => _completedSteps;
        private set => SetProperty(ref _completedSteps, value);
    }

    public int TotalSteps
    {
        get => _totalSteps;
        private set => SetProperty(ref _totalSteps, value);
    }

    public void Report(StartupProgress value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Message = value.Message;
        TotalSteps = value.TotalSteps;
        CompletedSteps = value.CompletedSteps;
    }
}
