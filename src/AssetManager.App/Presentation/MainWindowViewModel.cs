using System.Windows.Input;
using AssetManager.Application.Startup;

namespace AssetManager.App.Presentation;

public sealed class MainWindowViewModel : ObservableObject
{
    private string _statusMessage;

    public MainWindowViewModel(StartupResult startupResult)
    {
        ArgumentNullException.ThrowIfNull(startupResult);
        DataRoot = startupResult.DataRoot;
        RecordCount = startupResult.RecordCount;
        StartupSummary = startupResult.CreatedInitialData
            ? "初期データを作成し、利用準備が整いました。"
            : "既存の管理データを読み込みました。";
        _statusMessage = CreateStatusMessage(startupResult);
        ConfirmEnvironmentCommand = new RelayCommand(ConfirmEnvironment);
    }

    public string DataRoot { get; }

    public int RecordCount { get; }

    public string StartupSummary { get; }

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

    private static string CreateStatusMessage(StartupResult result)
    {
        if (result.ExcludedRecordCount > 0 || result.RepairedValueCount > 0)
        {
            return $"読み込み完了: 修復 {result.RepairedValueCount} 件 / 除外 {result.ExcludedRecordCount} 件";
        }

        return "管理データの読み込みが完了しました。";
    }
}
