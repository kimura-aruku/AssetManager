using AssetManager.Domain.Catalog;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Validation;
using AssetManager.Domain.Values;

namespace AssetManager.UnitTests.Domain;

public sealed class DomainModelValidatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 19, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordValidationFindsInvalidSelectionTypeAndTagReferences()
    {
        var selectionDefinition = FieldDefinition.CreateCustom(
            CustomFieldId.New(),
            "用途",
            FieldType.SingleSelect,
            options: [new SelectionOption(new SelectionOptionId("use.game"), "ゲーム")]);
        var typeDefinition = GetDefinition(BuiltInFieldIds.AssetTypes);
        var tagDefinition = GetDefinition(BuiltInFieldIds.Tags);
        var record = AssetRecord.Create(Now)
            .SetValue(
                selectionDefinition,
                new SingleSelectionFieldValue(new SelectionOptionId("use.video")),
                Now.AddMinutes(1))
            .SetValue(
                typeDefinition,
                new AssetTypeSetFieldValue([new AssetTypeId("type.unknown")]),
                Now.AddMinutes(2))
            .SetValue(
                tagDefinition,
                new TagSetFieldValue([new TagId("tag.unknown")]),
                Now.AddMinutes(3));

        var result = DomainModelValidator.ValidateRecord(
            record,
            BuiltInFieldCatalog.All.Append(selectionDefinition),
            [],
            []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == ValidationIssueCode.InvalidSelectionOptionReference);
        Assert.Contains(result.Issues, issue => issue.Code == ValidationIssueCode.InvalidAssetTypeReference);
        Assert.Contains(result.Issues, issue => issue.Code == ValidationIssueCode.InvalidTagReference);
    }

    [Fact]
    public void UnknownFieldIsPreservedWithoutInvalidatingKnownValues()
    {
        var unknownDefinition = FieldDefinition.CreateCustom(
            CustomFieldId.New(),
            "後で削除された欄",
            FieldType.Text);
        var record = AssetRecord.Create(Now).SetValue(
            unknownDefinition,
            new TextFieldValue("保持する値"),
            Now.AddMinutes(1));

        var result = DomainModelValidator.ValidateRecord(
            record,
            BuiltInFieldCatalog.All,
            [],
            []);

        Assert.True(result.IsValid);
        Assert.Equal([unknownDefinition.Id], result.UnknownFieldIds);
        Assert.Equal("保持する値", record.GetValue<TextFieldValue>(unknownDefinition.Id)?.Value);
    }

    [Fact]
    public void CreatorFieldRequiresCreatorSpecificValue()
    {
        var creatorDefinition = GetDefinition(BuiltInFieldIds.Creators);
        var record = AssetRecord.Create(Now).SetValue(
            creatorDefinition,
            new SellerListFieldValue(["販売者A"]),
            Now.AddMinutes(1));

        var result = DomainModelValidator.ValidateRecord(
            record,
            BuiltInFieldCatalog.All,
            [],
            []);

        Assert.Contains(result.Issues, issue => issue.Code == ValidationIssueCode.InvalidDedicatedValue);
    }

    [Fact]
    public void TagValidationFindsMissingCategory()
    {
        var tag = new TagDefinition(
            new TagId("tag.fantasy"),
            "ファンタジー",
            new TagColor("#123456"),
            new TagCategoryId("tag-category.genre"));

        var issues = DomainModelValidator.ValidateTagCategories([tag], []);

        Assert.Contains(issues, issue => issue.Code == ValidationIssueCode.InvalidTagCategoryReference);
    }

    private static FieldDefinition GetDefinition(FieldId id)
    {
        return BuiltInFieldCatalog.All.Single(definition => definition.Id == id);
    }
}
