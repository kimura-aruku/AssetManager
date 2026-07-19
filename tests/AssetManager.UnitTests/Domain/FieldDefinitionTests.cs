using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Validation;

namespace AssetManager.UnitTests.Domain;

public sealed class FieldDefinitionTests
{
    [Fact]
    public void BuiltInCatalogHasValidUniqueDefinitions()
    {
        var definitions = BuiltInFieldCatalog.All;

        var issues = DomainModelValidator.ValidateFieldDefinitions(definitions);

        Assert.Empty(issues);
        Assert.Equal(definitions.Count, definitions.Select(definition => definition.Id).Distinct().Count());
        Assert.Equal(4, MainTableColumns.Required.Count);
    }

    [Fact]
    public void RequiredFieldsCannotBeRenamedHiddenRetypedOrDeleted()
    {
        var name = BuiltInFieldCatalog.All.Single(definition => definition.Id == BuiltInFieldIds.Name);

        Assert.True(name.MainTableRequired);
        Assert.False(name.UserCanHide);
        Assert.False(name.UserCanRename);
        Assert.False(name.UserCanChangeType);
        Assert.False(name.UserCanDelete);
        Assert.Throws<DomainValidationException>(() => name.Rename("別名"));
        Assert.Throws<DomainValidationException>(() => name.ChangeType(FieldType.Number));
        Assert.Throws<DomainValidationException>(() => name.SetVisibility(false, false));
    }

    [Fact]
    public void CustomFieldSupportsAllowedChanges()
    {
        var original = FieldDefinition.CreateCustom(
            CustomFieldId.New(),
            "自由欄",
            FieldType.Text);

        var changed = original
            .Rename("数値欄")
            .ChangeType(FieldType.Number)
            .SetVisibility(true, false);

        Assert.Equal("数値欄", changed.Label);
        Assert.Equal(FieldType.Number, changed.Type);
        Assert.True(changed.MainTableVisible);
        Assert.False(changed.DetailVisible);
    }

    [Fact]
    public void CustomFieldRejectsBuiltInOnlyType()
    {
        Assert.Throws<DomainValidationException>(() => FieldDefinition.CreateCustom(
            CustomFieldId.New(),
            "対象パス",
            FieldType.TargetPath));
    }

    [Fact]
    public void SelectionFieldRejectsDuplicateOptionIds()
    {
        var id = new SelectionOptionId("option.one");

        Assert.Throws<DomainValidationException>(() => FieldDefinition.CreateCustom(
            CustomFieldId.New(),
            "選択欄",
            FieldType.SingleSelect,
            options:
            [
                new SelectionOption(id, "1"),
                new SelectionOption(id, "重複"),
            ]));
    }

    [Fact]
    public void ValidatorRejectsChangedBuiltInFieldType()
    {
        var changedName = FieldDefinition.CreateBuiltIn(
            BuiltInFieldIds.Name,
            "素材名",
            FieldType.Number,
            SystemRole.RecordName,
            mainTableRequired: true);
        var definitions = BuiltInFieldCatalog.All
            .Where(definition => definition.Id != BuiltInFieldIds.Name)
            .Append(changedName);

        var issues = DomainModelValidator.ValidateFieldDefinitions(definitions);

        Assert.Contains(
            issues,
            issue => issue.Code == ValidationIssueCode.InvalidBuiltInFieldDefinition);
    }
}
