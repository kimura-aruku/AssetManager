using AssetManager.App.Presentation;
using AssetManager.App.Windows;
using AssetManager.Application.Startup;
using AssetManager.Application.History;
using AssetManager.Application.Paths;
using AssetManager.Infrastructure.History;
using AssetManager.Infrastructure.Logging;
using AssetManager.Infrastructure.Operations;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Startup;
using AssetManager.Infrastructure.Windows;

namespace AssetManager.App.Composition;

internal static class AppCompositionRoot
{
    public static AppServices CreateServices()
    {
        var paths = AppDataPaths.CreateDefault();
        var logger = new RollingFileLogger(paths.LogsDirectory);
        return new AppServices(
            logger,
            new DataSetInitializer(paths));
    }

    public static StartupWindow CreateStartupWindow(StartupWindowViewModel viewModel)
    {
        return new StartupWindow
        {
            DataContext = viewModel,
        };
    }

    public static MainWindow CreateMainWindow(
        StartupResult startupResult,
        RecordPathCheckCoordinator pathChecks)
    {
        var viewModel = new MainWindowViewModel(startupResult, pathChecks);

        return new MainWindow
        {
            DataContext = viewModel,
        };
    }

    public static AppRuntimeServices CreateRuntimeServices(StartupResult startupResult)
    {
        ArgumentNullException.ThrowIfNull(startupResult);
        var layout = new DataRootLayout(startupResult.DataRoot);
        var store = new JsonAssetManagerDataStore(layout);
        var fileSystem = new PhysicalWindowsPathFileSystem();
        return new AppRuntimeServices(
            new UndoRedoService(
                store,
                new FileUndoHistoryPersistence(layout)),
            new RecordPathCheckCoordinator(
                store,
                new PathCheckService(fileSystem)),
            new PathRegistrationService(fileSystem, new WpfWindowsPathPicker()),
            new WindowsShellService());
    }
}

internal sealed record AppServices(
    RollingFileLogger Logger,
    IStartupInitializer StartupInitializer);

internal sealed record AppRuntimeServices(
    UndoRedoService UndoRedo,
    RecordPathCheckCoordinator PathChecks,
    PathRegistrationService PathRegistration,
    IWindowsShellService Shell);
