using System.Windows.Input;
using AssetManager.Application.Paths;
using AssetManager.Application.Startup;

namespace AssetManager.App.Presentation;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly RecordPathCheckCoordinator _pathChecks;
    private string _statusMessage;
    private CancellationTokenSource? _pathCheckCancellation;
    private bool _isCheckingPaths;

    public MainWindowViewModel(
        StartupResult startupResult,
        RecordPathCheckCoordinator pathChecks)
    {
        ArgumentNullException.ThrowIfNull(startupResult);
        _pathChecks = pathChecks ?? throw new ArgumentNullException(nameof(pathChecks));
        DataRoot = startupResult.DataRoot;
        RecordCount = startupResult.RecordCount;
        StartupSummary = startupResult.CreatedInitialData
            ? "初期データを作成し、利用準備が整いました。"
            : "既存の管理データを読み込みました。";
        _statusMessage = CreateStatusMessage(startupResult);
        ConfirmEnvironmentCommand = new RelayCommand(ConfirmEnvironment);
        CheckAllPathsCommand = new AsyncRelayCommand(
            () => CheckAllPathsAsync(refreshCachedResults: true),
            () => !IsCheckingPaths);
        CancelPathCheckCommand = new RelayCommand(
            CancelPathCheck,
            () => IsCheckingPaths);
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

    public ICommand CheckAllPathsCommand { get; }

    public ICommand CancelPathCheckCommand { get; }

    public bool IsCheckingPaths
    {
        get => _isCheckingPaths;
        private set
        {
            if (SetProperty(ref _isCheckingPaths, value))
            {
                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public Task StartInitialPathCheckAsync()
    {
        return CheckAllPathsAsync(refreshCachedResults: false);
    }

    public void Dispose()
    {
        _pathCheckCancellation?.Cancel();
        _pathCheckCancellation?.Dispose();
        _pathCheckCancellation = null;
    }

    private void ConfirmEnvironment()
    {
        StatusMessage = $"準備状態を確認しました（{DateTime.Now:yyyy/MM/dd HH:mm:ss}）。";
    }

    private async Task CheckAllPathsAsync(bool refreshCachedResults)
    {
        if (IsCheckingPaths)
        {
            return;
        }

        _pathCheckCancellation?.Dispose();
        _pathCheckCancellation = new CancellationTokenSource();
        IsCheckingPaths = true;
        var progress = new Progress<PathCheckProgress>(item =>
        {
            StatusMessage = item.IsCanceled
                ? $"パス確認をキャンセルしました（未確認 {item.UncheckedCount} 件）。"
                : $"パスを確認しています（{item.CheckedCount} / {item.TotalCount}）。";
        });
        try
        {
            var result = await _pathChecks.CheckAllRecordsAsync(
                refreshCachedResults,
                progress,
                _pathCheckCancellation.Token);
            StatusMessage = result.IsCanceled
                ? $"パス確認をキャンセルしました（未確認 {result.TotalCount - result.CheckedCount} 件）。"
                : $"パス確認が完了しました（{result.CheckedCount} 件）。";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "パス確認をキャンセルしました。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"パス確認に失敗しました: {exception.Message}";
        }
        finally
        {
            IsCheckingPaths = false;
        }
    }

    private void CancelPathCheck()
    {
        _pathCheckCancellation?.Cancel();
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
