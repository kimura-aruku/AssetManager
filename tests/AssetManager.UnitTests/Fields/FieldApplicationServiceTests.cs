using AssetManager.Application.Data;
using AssetManager.Application.Fields;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;
using AssetManager.UnitTests.Testing;

namespace AssetManager.UnitTests.Fields;

public sealed class FieldApplicationServiceTests
{
    private static readonly DateTimeOffset TestTime = new(
        2026,
        7,
        19,
        12,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public async Task CustomFieldCanBeAddedRenamedAndHidden()
    {
        var store = CreateStore();
        var service = CreateService(store);

        var added = await service.AddCustomAsync("メモ欄", FieldType.Text, mainTableVisible: true);
        var renamed = await service.RenameAsync(added.Id, "補足");
        var hidden = await service.SetVisibilityAsync(added.Id, false, false);

        Assert.True(added.Id.IsCustom);
        Assert.Equal("補足", renamed.Label);
        Assert.False(hidden.MainTableVisible);
        Assert.False(hidden.DetailVisible);
    }

    [Fact]
    public async Task BuiltInFieldConstraintsAreEnforced()
    {
        var store = CreateStore();
        var service = CreateService(store);

        _ = await Assert.ThrowsAsync<DomainValidationException>(
            () => service.RenameAsync(BuiltInFieldIds.Name, "別名"));
        _ = await Assert.ThrowsAsync<DomainValidationException>(
            () => service.SetVisibilityAsync(BuiltInFieldIds.Name, false, false));
        _ = await Assert.ThrowsAsync<DomainValidationException>(
            () => service.DeleteCustomAsync(BuiltInFieldIds.Name));
    }

    [Fact]
    public async Task ConvertibleTypeChangeUpdatesDefinitionAndEveryValueTransactionally()
    {
        var field = FieldDefinition.CreateCustom(CustomFieldId.New(), "数値", FieldType.Text);
        var first = CreateRecord(field, new TextFieldValue("123.5"));
        var second = CreateRecord(field, new TextFieldValue("42"));
        var store = CreateStore([field], [first, second]);
        var service = CreateService(store);

        var analysis = await service.AnalyzeTypeChangeAsync(field.Id, FieldType.Number);
        var status = await service.ChangeTypeAsync(field.Id, FieldType.Number, false);

        Assert.True(analysis.CanConvertAllValues);
        Assert.Equal(FieldTypeChangeStatus.Applied, status);
        Assert.Equal(1, store.TransactionSaveCount);
        Assert.All(store.Snapshot.Records, record =>
            Assert.IsType<NumberFieldValue>(record.Values[field.Id]));
    }

    [Fact]
    public async Task IncompatibleTypeChangeRequiresConfirmationThenClearsAllValues()
    {
        var field = FieldDefinition.CreateCustom(CustomFieldId.New(), "数値", FieldType.Text);
        var convertible = CreateRecord(field, new TextFieldValue("123"));
        var incompatible = CreateRecord(field, new TextFieldValue("変換不可"));
        var store = CreateStore([field], [convertible, incompatible]);
        var service = CreateService(store);

        var analysis = await service.AnalyzeTypeChangeAsync(field.Id, FieldType.Number);
        var pending = await service.ChangeTypeAsync(field.Id, FieldType.Number, false);

        Assert.False(analysis.CanConvertAllValues);
        Assert.Equal([incompatible.Id], analysis.IncompatibleRecordIds);
        Assert.Equal(FieldTypeChangeStatus.RequiresValueClearConfirmation, pending);
        Assert.Equal(0, store.TransactionSaveCount);

        var applied = await service.ChangeTypeAsync(field.Id, FieldType.Number, true);

        Assert.Equal(FieldTypeChangeStatus.Applied, applied);
        Assert.All(store.Snapshot.Records, record => Assert.False(record.Values.ContainsKey(field.Id)));
    }

    [Fact]
    public async Task DeleteCustomFieldRemovesDefinitionAndAllValuesTransactionally()
    {
        var field = FieldDefinition.CreateCustom(CustomFieldId.New(), "削除対象", FieldType.Text);
        var record = CreateRecord(field, new TextFieldValue("値"));
        var store = CreateStore([field], [record]);
        var service = CreateService(store);

        await service.DeleteCustomAsync(field.Id);

        Assert.DoesNotContain(store.Snapshot.FieldDefinitions, item => item.Id == field.Id);
        Assert.False(Assert.Single(store.Snapshot.Records).Values.ContainsKey(field.Id));
        Assert.Equal(1, store.TransactionSaveCount);
    }

    private static FieldApplicationService CreateService(TestDataStore store)
    {
        return new FieldApplicationService(store, new FixedTimeProvider(TestTime));
    }

    private static TestDataStore CreateStore(
        IReadOnlyList<FieldDefinition>? customFields = null,
        IReadOnlyList<AssetRecord>? records = null)
    {
        return new TestDataStore(new AssetManagerDataSnapshot(
            BuiltInFieldCatalog.All.Concat(customFields ?? []).ToArray(),
            [],
            [],
            [],
            records ?? []));
    }

    private static AssetRecord CreateRecord(FieldDefinition field, FieldValue value)
    {
        return AssetRecord.Create(TestTime).SetValue(field, value, TestTime);
    }
}
