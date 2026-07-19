using AssetManager.Application.GridEditing;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Values;

namespace AssetManager.UnitTests.GridEditing;

public sealed class GridBatchEditPlannerTests
{
    [Fact]
    public void CreateUniformUpdatesは編集したカラムだけを全選択行へ適用する()
    {
        var first = RecordId.New();
        var second = RecordId.New();
        var changes = new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("一括変更"),
        };

        var result = GridBatchEditPlanner.CreateUniformUpdates([first, second], changes);

        Assert.Equal(2, result.Count);
        Assert.All(result.Values, fields =>
        {
            var value = Assert.IsType<TextFieldValue>(Assert.Single(fields).Value);
            Assert.Equal("一括変更", value.Value);
        });
    }

    [Fact]
    public void CreateUniformUpdatesは対象パスの複数行適用を拒否する()
    {
        var changes = new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.TargetPath] = new TargetPathFieldValue(TargetPathKind.File, @"C:\Assets\item.png"),
        };

        var exception = Assert.Throws<GridClipboardException>(() =>
            GridBatchEditPlanner.CreateUniformUpdates([RecordId.New(), RecordId.New()], changes));

        Assert.Contains("対象パス", exception.Message, StringComparison.Ordinal);
    }
}
