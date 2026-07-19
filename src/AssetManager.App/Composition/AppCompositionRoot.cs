using AssetManager.App.Presentation;
using AssetManager.Application.Startup;
using AssetManager.Infrastructure.Logging;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Startup;

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

    public static MainWindow CreateMainWindow(StartupResult startupResult)
    {
        var viewModel = new MainWindowViewModel(startupResult);

        return new MainWindow
        {
            DataContext = viewModel,
        };
    }
}

internal sealed record AppServices(
    RollingFileLogger Logger,
    IStartupInitializer StartupInitializer);
