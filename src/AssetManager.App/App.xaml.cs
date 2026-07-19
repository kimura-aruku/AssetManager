using AssetManager.App.Composition;
using System.Windows;

namespace AssetManager.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = AppCompositionRoot.CreateMainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
