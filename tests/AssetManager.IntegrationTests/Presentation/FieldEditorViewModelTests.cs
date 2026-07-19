using AssetManager.App.Presentation;
using AssetManager.Domain.Fields;

namespace AssetManager.IntegrationTests.Presentation;

public sealed class FieldEditorViewModelTests
{
    [Fact]
    public void ReplaceOptionsは追加されたタグを編集中の詳細ペインへ反映する()
    {
        var definition = BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.Tags);
        var editor = new FieldEditorViewModel(definition, value: null);

        editor.ReplaceOptions(
        [
            new SelectableOptionViewModel("tag.fantasy", "ファンタジー"),
        ]);

        var option = Assert.Single(editor.Options);
        Assert.Equal("tag.fantasy", option.Id);
        Assert.Equal("ファンタジー", option.Label);
        Assert.False(option.IsSelected);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void ReplaceOptionsは既に選択中のタグを維持する()
    {
        var definition = BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.Tags);
        var selected = new SelectableOptionViewModel("tag.selected", "選択済み");
        var editor = new FieldEditorViewModel(definition, value: null, [selected]);
        selected.IsSelected = true;

        editor.ReplaceOptions(
        [
            new SelectableOptionViewModel("tag.selected", "選択済み"),
            new SelectableOptionViewModel("tag.added", "追加タグ"),
        ]);

        Assert.True(editor.Options.Single(option => option.Id == "tag.selected").IsSelected);
        Assert.False(editor.Options.Single(option => option.Id == "tag.added").IsSelected);
        Assert.True(editor.IsDirty);
    }
}
