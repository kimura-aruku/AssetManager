using System.Windows;
using AssetManager.App.Presentation;

namespace AssetManager.App;

public partial class ManagementWindow : Window
{
    public ManagementWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is ManagementWindowViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}
