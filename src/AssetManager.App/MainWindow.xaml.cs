using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AssetManager.App.Presentation;
using AssetManager.Application.GridEditing;
using AssetManager.Domain.Identifiers;

namespace AssetManager.App;

public partial class MainWindow : Window
{
    private readonly List<DataGridColumn> _dynamicColumns = [];
    private bool _isNormalizingSelection;
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
        foreach (var column in _dynamicColumns)
        {
            _ = RecordsGrid.Columns.Remove(column);
        }

        _dynamicColumns.Clear();

        foreach (var option in viewModel.GetVisibleDynamicColumns())
        {
            var column = new DataGridTextColumn
            {
                Header = option.Label,
                Width = 140,
                SortMemberPath = option.Definition.Id.Value,
                Binding = new Binding($"DynamicValues[{option.Definition.Id.Value}]")
                {
                    Mode = BindingMode.OneWay,
                },
            };
            var lastCheckedColumnIndex = RecordsGrid.Columns.IndexOf(LicenseLastCheckedColumn);
            RecordsGrid.Columns.Insert(lastCheckedColumnIndex, column);
            _dynamicColumns.Add(column);
        }
    }

    private void OnSelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        if (_isNormalizingSelection)
        {
            return;
        }

        if (RecordsGrid.SelectedCells.Count == 0)
        {
            UpdateViewModelSelection();
            return;
        }

        var shape = GridSelectionNormalizer.Normalize(RecordsGrid.SelectedCells
            .Where(cell => cell.Item is RecordRowViewModel)
            .Select(cell => new GridCellPosition(
                RecordsGrid.Items.IndexOf(cell.Item),
                cell.Column.DisplayIndex)));
        if (shape.RowIndexes.Count == 0 || shape.ColumnIndexes.Count == 0)
        {
            UpdateViewModelSelection();
            return;
        }

        var rows = shape.RowIndexes
            .Select(index => RecordsGrid.Items[index])
            .OfType<RecordRowViewModel>()
            .ToArray();
        var columns = RecordsGrid.Columns
            .Where(column => shape.ColumnIndexes.Contains(column.DisplayIndex))
            .OrderBy(column => column.DisplayIndex)
            .ToArray();
        if (RecordsGrid.SelectedCells.Count != shape.CellCount)
        {
            _isNormalizingSelection = true;
            try
            {
                RecordsGrid.SelectedCells.Clear();
                foreach (var row in rows)
                {
                    foreach (var column in columns)
                    {
                        RecordsGrid.SelectedCells.Add(new DataGridCellInfo(row, column));
                    }
                }
            }
            finally
            {
                _isNormalizingSelection = false;
            }
        }

        UpdateViewModelSelection();
    }

    private void OnGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var header = FindAncestor<DataGridColumnHeader>(e.OriginalSource as DependencyObject);
        if (header?.Column is null)
        {
            return;
        }

        e.Handled = true;
        _isNormalizingSelection = true;
        try
        {
            RecordsGrid.SelectedCells.Clear();
            foreach (var row in RecordsGrid.Items.OfType<RecordRowViewModel>())
            {
                RecordsGrid.SelectedCells.Add(new DataGridCellInfo(row, header.Column));
            }
        }
        finally
        {
            _isNormalizingSelection = false;
        }

        UpdateViewModelSelection();
    }

    private void OnTargetPathSelectClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnUrlInputLostFocus(object sender, RoutedEventArgs e)
    {
        var current = sender as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: FieldEditorViewModel editor })
            {
                editor.ValidateUrlInput();
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }

    private void UpdateViewModelSelection()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var rows = RecordsGrid.SelectedCells
            .Select(cell => cell.Item)
            .OfType<RecordRowViewModel>()
            .Distinct()
            .OrderBy(row => RecordsGrid.Items.IndexOf(row))
            .ToArray();
        if (rows.Length > 0 && !ReferenceEquals(viewModel.SelectedRecord, rows[0]))
        {
            viewModel.SelectedRecord = rows[0];
        }

        var fields = RecordsGrid.SelectedCells
            .Select(cell => cell.Column)
            .Distinct()
            .OrderBy(column => column.DisplayIndex)
            .Select(column => TryCreateFieldId(column.SortMemberPath))
            .Where(fieldId => fieldId is not null)
            .Select(fieldId => fieldId!.Value)
            .ToArray();
        viewModel.SetGridSelection(rows, fields);
    }

    private static FieldId? TryCreateFieldId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("computed.", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return new FieldId(value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T result)
            {
                return result;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
