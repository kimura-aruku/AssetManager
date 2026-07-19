namespace AssetManager.Application.GridEditing;

public readonly record struct GridCellPosition(int RowIndex, int ColumnIndex);

public sealed record GridSelectionShape(
    IReadOnlyList<int> RowIndexes,
    IReadOnlyList<int> ColumnIndexes)
{
    public int CellCount => RowIndexes.Count * ColumnIndexes.Count;
}

public static class GridSelectionNormalizer
{
    public static GridSelectionShape Normalize(IEnumerable<GridCellPosition> selectedCells)
    {
        ArgumentNullException.ThrowIfNull(selectedCells);
        var cells = selectedCells.Distinct().ToArray();
        if (cells.Length == 0)
        {
            return new GridSelectionShape([], []);
        }

        if (cells.Any(cell => cell.RowIndex < 0 || cell.ColumnIndex < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(selectedCells), "行・カラム番号は0以上にしてください。");
        }

        var rows = cells.Select(cell => cell.RowIndex).Distinct().Order().ToArray();
        var firstColumn = cells.Min(cell => cell.ColumnIndex);
        var lastColumn = cells.Max(cell => cell.ColumnIndex);
        var columns = Enumerable.Range(firstColumn, lastColumn - firstColumn + 1).ToArray();
        return new GridSelectionShape(rows, columns);
    }
}
