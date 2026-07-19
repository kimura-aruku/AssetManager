using AssetManager.App.Presentation;
using AssetManager.App.Windows;
using AssetManager.Application.Catalog;
using AssetManager.Application.Data;
using AssetManager.Application.Fields;
using AssetManager.Application.GridEditing;
using AssetManager.Application.Records;
using AssetManager.Application.Settings;
using AssetManager.Application.Startup;
using AssetManager.Application.History;
using AssetManager.Application.Paths;
using AssetManager.Application.Search;
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
        AppRuntimeServices runtime)
    {
        var dialogs = new WpfUserDialogService();
        var viewModel = new MainWindowViewModel(
            startupResult,
            runtime,
            dialogs,
            new WpfClipboardService());

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
        var undoRedo = new UndoRedoService(
            store,
            new FileUndoHistoryPersistence(layout));
        var fields = new FieldApplicationService(store, history: undoRedo);
        var catalog = new CatalogApplicationService(store);
        var records = new RecordApplicationService(store, history: undoRedo);
        return new AppRuntimeServices(
            store,
            undoRedo,
            records,
            new GridClipboardService(store, records),
            fields,
            catalog,
            new RecordPathCheckCoordinator(
                store,
                new PathCheckService(fileSystem)),
            new PathRegistrationService(fileSystem, new WpfWindowsPathPicker()),
            new WindowsShellService(),
            new SearchConfigurationService(new JsonViewConfigurationStore(layout)),
            new AppSettingsService(new JsonAppSettingsStore(layout)),
            () =>
            {
                var window = new ManagementWindow
                {
                    DataContext = new ManagementWindowViewModel(
                        store,
                        fields,
                        catalog,
                        new WpfUserDialogService()),
                    Owner = System.Windows.Application.Current.MainWindow,
                };
                _ = window.ShowDialog();
            });
    }
}

internal sealed record AppServices(
    RollingFileLogger Logger,
    IStartupInitializer StartupInitializer);

internal sealed record AppRuntimeServices(
    IAssetManagerDataStore Store,
    UndoRedoService UndoRedo,
    RecordApplicationService Records,
    GridClipboardService GridClipboard,
    FieldApplicationService Fields,
    CatalogApplicationService Catalog,
    RecordPathCheckCoordinator PathChecks,
    PathRegistrationService PathRegistration,
    IWindowsShellService Shell,
    SearchConfigurationService SearchConfiguration,
    AppSettingsService Settings,
    Action ShowManagementWindow);
