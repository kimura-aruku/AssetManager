using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AssetManager.App.Presentation;

namespace AssetManager.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.GridColumnsChanged -= OnGridColumnsChanged;
        }

        (DataContext as IDisposable)?.Dispose();
        base.OnClosed(e);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainWindowViewModel oldViewModel)
        {
            oldViewModel.GridColumnsChanged -= OnGridColumnsChanged;
        }

        if (e.NewValue is MainWindowViewModel newViewModel)
        {
            newViewModel.GridColumnsChanged += OnGridColumnsChanged;
            RebuildDynamicColumns(newViewModel);
        }
    }

    private void OnGridColumnsChanged(object? sender, EventArgs e)
    {
        if (sender is MainWindowViewModel viewModel)
        {
            Dispatcher.Invoke(() => RebuildDynamicColumns(viewModel));
        }
    }

    private void RebuildDynamicColumns(MainWindowViewModel viewModel)
    {
        while (RecordsGrid.Columns.Count > 5)
        {
            RecordsGrid.Columns.RemoveAt(RecordsGrid.Columns.Count - 1);
        }

        foreach (var option in viewModel.GetVisibleDynamicColumns())
        {
            RecordsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = option.Label,
                Width = 140,
                Binding = new Binding($"DynamicValues[{option.Definition.Id.Value}]")
                {
                    Mode = BindingMode.OneWay,
                },
            });
        }
    }
}
