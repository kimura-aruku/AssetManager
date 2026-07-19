using AssetManager.App.Composition;
using AssetManager.App.Presentation;
using AssetManager.Application.Startup;
using AssetManager.Application.History;
using AssetManager.Infrastructure.Logging;
using AssetManager.Infrastructure.Windows;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Threading;
using System.Windows;

namespace AssetManager.App;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF Applicationの終了ライフサイクルでOnExitから破棄します。")]
public partial class App : System.Windows.Application
{
    private const string ApplicationId = "kimura-aruku.AssetManager";
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private SingleInstanceCoordinator? _singleInstance;
    private RollingFileLogger? _logger;
    private Task? _activationListener;
    private UndoRedoService? _undoRedo;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _singleInstance = new SingleInstanceCoordinator(ApplicationId);
        if (!_singleInstance.IsPrimary)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        var services = AppCompositionRoot.CreateServices();
        _logger = services.Logger;
        RegisterExceptionHandlers();

        try
        {
            await _logger.InitializeAsync(_lifetimeCancellation.Token);
            await _logger.LogInformationAsync(
                "アプリケーションを起動しました。",
                _lifetimeCancellation.Token);

            var startupViewModel = new StartupWindowViewModel();
            var startupProgress = new Progress<StartupProgress>(startupViewModel.Report);
            var startupWindow = AppCompositionRoot.CreateStartupWindow(startupViewModel);
            MainWindow = startupWindow;
            startupWindow.Show();

            var result = await services.StartupInitializer.InitializeAsync(
                startupProgress,
                _lifetimeCancellation.Token);
            var runtime = AppCompositionRoot.CreateRuntimeServices(result);
            _undoRedo = runtime.UndoRedo;
            await _undoRedo.InitializeAsync(_lifetimeCancellation.Token);

            var mainWindow = AppCompositionRoot.CreateMainWindow(result, runtime.PathChecks);
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
            startupWindow.Close();
            if (result.CheckPathsOnStartup
                && mainWindow.DataContext is MainWindowViewModel mainWindowViewModel)
            {
                _ = mainWindowViewModel.StartInitialPathCheckAsync();
            }

            _activationListener = _singleInstance.ListenForActivationAsync(
                ActivateMainWindow,
                _lifetimeCancellation.Token);
            await _logger.LogInformationAsync(
                $"起動処理が完了しました。レコード数: {result.RecordCount}",
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            Shutdown();
        }
        catch (Exception exception)
        {
            await TryLogExceptionAsync("起動処理に失敗しました。", exception);
            ShowStartupError(exception);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _lifetimeCancellation.Cancel();
        if (_activationListener is not null)
        {
            try
            {
                _activationListener.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException exception) when (
                exception.InnerExceptions.All(item => item is OperationCanceledException))
            {
            }
        }

        if (_undoRedo is not null)
        {
            try
            {
                _undoRedo.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                TryLogExceptionAsync(
                    "アンドゥ履歴の終了処理に失敗しました。",
                    exception).GetAwaiter().GetResult();
            }
        }

        _singleInstance?.Dispose();
        _logger?.Dispose();
        _lifetimeCancellation.Dispose();
        base.OnExit(e);
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private async void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        await TryLogExceptionAsync("UIスレッドで未処理例外が発生しました。", e.Exception);
        e.Handled = true;
        _ = MessageBox.Show(
            "予期しないエラーが発生しました。詳細はログを確認してください。アプリを終了します。",
            "AssetManager - エラー",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(-1);
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            TryLogExceptionAsync(
                "アプリケーションで未処理例外が発生しました。",
                exception).GetAwaiter().GetResult();
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _ = TryLogExceptionAsync(
            "バックグラウンド処理で未処理例外が発生しました。",
            e.Exception);
        e.SetObserved();
    }

    private void ActivateMainWindow()
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            var window = MainWindow;
            if (window is null)
            {
                return;
            }

            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Show();
            window.Activate();
            window.Topmost = true;
            window.Topmost = false;
            window.Focus();
        });
    }

    private async Task TryLogExceptionAsync(string context, Exception exception)
    {
        if (_logger is null)
        {
            return;
        }

        try
        {
            await _logger.LogErrorAsync(context, exception);
        }
        catch (Exception)
        {
        }
    }

    private static void ShowStartupError(Exception exception)
    {
        var failure = exception as StartupException;
        var title = failure?.FailureKind == StartupFailureKind.DataLoading
            ? "AssetManager - 読み込みエラー"
            : "AssetManager - データ作成エラー";
        var action = failure?.FailureKind == StartupFailureKind.DataLoading
            ? "管理データを読み込めませんでした。"
            : "初期データを準備できませんでした。";
        _ = MessageBox.Show(
            $"{action}{Environment.NewLine}詳細はログを確認してください。{Environment.NewLine}{exception.Message}",
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
