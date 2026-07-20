using AssetManager.App.Presentation;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Values;

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

    [Fact]
    public void リスト入力は空の1行から開始し0行にはできない()
    {
        var definition = BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.Creators);
        var editor = new FieldEditorViewModel(definition, value: null);

        Assert.Single(editor.Entries);
        Assert.False(editor.RemoveListEntryCommand.CanExecute(null));

        editor.AddListEntryCommand.Execute(null);
        Assert.Equal(2, editor.Entries.Count);
        Assert.True(editor.RemoveListEntryCommand.CanExecute(null));

        editor.RemoveListEntryCommand.Execute(null);
        Assert.Single(editor.Entries);
        Assert.False(editor.RemoveListEntryCommand.CanExecute(null));
    }

    [Fact]
    public void 制作者は入力欄ごとの値として保存する()
    {
        var definition = BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.Creators);
        var editor = new FieldEditorViewModel(
            definition,
            new CreatorListFieldValue(["制作者A", "制作者B"]));

        Assert.Equal(["制作者A", "制作者B"], editor.Entries.Select(entry => entry.PrimaryText));

        editor.Entries[1].PrimaryText = "制作者C";
        var value = Assert.IsType<CreatorListFieldValue>(editor.CreateValue());
        Assert.Equal(["制作者A", "制作者C"], value.Values);
    }

    [Fact]
    public void 関連文書と関連URLはタイトルと値を行単位で保存する()
    {
        var documentDefinition = BuiltInFieldCatalog.All.Single(
            field => field.Id == BuiltInFieldIds.RelatedDocuments);
        var documentEditor = new FieldEditorViewModel(documentDefinition, value: null);
        documentEditor.Entries[0].PrimaryText = "利用規約";
        documentEditor.Entries[0].SecondaryText = @"C:\Assets\license.txt";

        var documents = Assert.IsType<RelatedDocumentListFieldValue>(documentEditor.CreateValue());
        var document = Assert.Single(documents.Values);
        Assert.Equal("利用規約", document.Title);
        Assert.Equal(@"C:\Assets\license.txt", document.Path);

        var urlDefinition = BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.RelatedUrls);
        var urlEditor = new FieldEditorViewModel(urlDefinition, value: null);
        urlEditor.Entries[0].PrimaryText = "商品ページ";
        urlEditor.Entries[0].SecondaryText = "https://example.invalid/product";

        var urls = Assert.IsType<RelatedUrlListFieldValue>(urlEditor.CreateValue());
        var url = Assert.Single(urls.Values);
        Assert.Equal("商品ページ", url.Title);
        Assert.Equal("https://example.invalid/product", url.Url);
    }

    [Fact]
    public void URLは明示的な検証後に書式判定を表示する()
    {
        var definition = BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.ProductUrl);
        var editor = new FieldEditorViewModel(definition, value: null);

        editor.Text = "https://example.invalid/product";

        Assert.False(editor.ShowsValidUrlIndicator);
        Assert.False(editor.ShowsInvalidUrlIndicator);

        editor.ValidateUrlInput();

        Assert.True(editor.ShowsValidUrlIndicator);
        Assert.False(editor.ShowsInvalidUrlIndicator);
        Assert.Null(editor.Warning);

        editor.Text = "file:///C:/Assets/product.html";

        Assert.False(editor.ShowsValidUrlIndicator);
        Assert.False(editor.ShowsInvalidUrlIndicator);

        editor.ValidateUrlInput();

        Assert.False(editor.ShowsValidUrlIndicator);
        Assert.True(editor.ShowsInvalidUrlIndicator);
        Assert.StartsWith("HTTPまたはHTTPSのURLを指定してください。", editor.Warning);
    }

    [Fact]
    public void 関連URLは入力行全体をまとめて書式判定する()
    {
        var definition = BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.RelatedUrls);
        var editor = new FieldEditorViewModel(definition, value: null);
        editor.Entries[0].PrimaryText = "商品ページ";
        editor.Entries[0].SecondaryText = "https://example.invalid/product";

        editor.ValidateUrlInput();

        Assert.True(editor.ShowsValidUrlIndicator);
        Assert.False(editor.ShowsInvalidUrlIndicator);

        editor.Entries[0].PrimaryText = string.Empty;
        editor.ValidateUrlInput();

        Assert.False(editor.ShowsValidUrlIndicator);
        Assert.True(editor.ShowsInvalidUrlIndicator);
        Assert.Contains("タイトルと値を両方入力してください。", editor.Warning);
    }

    [Fact]
    public void 種類とその他の複数選択を別レイアウトとして識別する()
    {
        var typeDefinition = BuiltInFieldCatalog.All.Single(
            field => field.Id == BuiltInFieldIds.AssetTypes);
        var tagDefinition = BuiltInFieldCatalog.All.Single(
            field => field.Id == BuiltInFieldIds.Tags);

        var typeEditor = new FieldEditorViewModel(typeDefinition, value: null);
        var tagEditor = new FieldEditorViewModel(tagDefinition, value: null);

        Assert.True(typeEditor.IsAssetTypeSet);
        Assert.False(typeEditor.IsTagSet);
        Assert.False(typeEditor.IsOtherMultiOption);
        Assert.False(tagEditor.IsAssetTypeSet);
        Assert.True(tagEditor.IsTagSet);
        Assert.False(tagEditor.IsOtherMultiOption);
    }

    [Fact]
    public void ライセンス条件をグループ表示対象として識別する()
    {
        var licenseEditors = BuiltInFieldCatalog.All
            .Select(definition => new FieldEditorViewModel(definition, value: null))
            .Where(editor => editor.IsLicenseCondition)
            .ToArray();
        var favoriteDefinition = BuiltInFieldCatalog.All.Single(
            field => field.Id == BuiltInFieldIds.Favorite);
        var favoriteEditor = new FieldEditorViewModel(favoriteDefinition, value: null);

        Assert.Equal(10, licenseEditors.Length);
        Assert.All(licenseEditors, editor => Assert.False(editor.IsStandaloneBoolean));
        Assert.False(licenseEditors[0].IsDetailItemVisible);

        licenseEditors[0].ShowLicenseConditionGroup();

        Assert.True(licenseEditors[0].ShowsLicenseConditionGroup);
        Assert.True(licenseEditors[0].IsDetailItemVisible);
        Assert.True(favoriteEditor.IsStandaloneBoolean);
        Assert.True(favoriteEditor.IsDetailItemVisible);
    }
}
