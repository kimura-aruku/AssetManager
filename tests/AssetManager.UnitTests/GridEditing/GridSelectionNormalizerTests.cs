using AssetManager.Application.GridEditing;

namespace AssetManager.UnitTests.GridEditing;

public sealed class GridSelectionNormalizerTests
{
    [Fact]
    public void Normalizeは非連続行へ同じ連続カラム範囲を適用する()
    {
        var result = GridSelectionNormalizer.Normalize(
        [
            new GridCellPosition(1, 2),
            new GridCellPosition(1, 4),
            new GridCellPosition(5, 3),
        ]);

        Assert.Equal([1, 5], result.RowIndexes);
        Assert.Equal([2, 3, 4], result.ColumnIndexes);
        Assert.Equal(6, result.CellCount);
    }

    [Fact]
    public void Normalizeは重複セルを除外する()
    {
        var result = GridSelectionNormalizer.Normalize(
        [
            new GridCellPosition(0, 1),
            new GridCellPosition(0, 1),
        ]);

        Assert.Equal([0], result.RowIndexes);
        Assert.Equal([1], result.ColumnIndexes);
        Assert.Equal(1, result.CellCount);
    }

    [Fact]
    public void Normalizeは空選択を空の形状として返す()
    {
        var result = GridSelectionNormalizer.Normalize([]);

        Assert.Empty(result.RowIndexes);
        Assert.Empty(result.ColumnIndexes);
        Assert.Equal(0, result.CellCount);
    }
}
