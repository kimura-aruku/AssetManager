using System.Windows;
using AssetManager.App.Presentation;

namespace AssetManager.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}
