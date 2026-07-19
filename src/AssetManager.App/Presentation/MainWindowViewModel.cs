using System.Windows.Input;

namespace AssetManager.App.Presentation;

public sealed class MainWindowViewModel : ObservableObject
{
    private string _statusMessage = ".NET 10 / WPF / MVVM 構成で起動しています。";

    public MainWindowViewModel()
    {
        ConfirmEnvironmentCommand = new RelayCommand(ConfirmEnvironment);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand ConfirmEnvironmentCommand { get; }

    private void ConfirmEnvironment()
    {
        StatusMessage = $"準備状態を確認しました（{DateTime.Now:yyyy/MM/dd HH:mm:ss}）。";
    }
}
