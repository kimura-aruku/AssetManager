using AssetManager.App.Presentation;

namespace AssetManager.App.Composition;

internal static class AppCompositionRoot
{
    public static MainWindow CreateMainWindow()
    {
        var viewModel = new MainWindowViewModel();

        return new MainWindow
        {
            DataContext = viewModel,
        };
    }
}
