using AssetManager.Application.Catalog;
using AssetManager.Application.Data;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;
using AssetManager.UnitTests.Testing;

namespace AssetManager.UnitTests.Catalog;

public sealed class AcquisitionSourceMigrationServiceTests
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
    public async Task MigrateAsyncは既存の自由入力値をマスタと選択値へ変換する()
    {
        var legacyDefinition = CreateLegacyDefinition();
        var definitions = BuiltInFieldCatalog.All.Select(definition =>
            definition.Id == BuiltInFieldIds.AcquisitionSource
                ? legacyDefinition
                : definition).ToArray();
        var first = AssetRecord.Create(TestTime).SetValue(
            legacyDefinition,
            new TextFieldValue("BOOTH"),
            TestTime);
        var second = AssetRecord.Create(TestTime).SetValue(
            legacyDefinition,
            new TextFieldValue("booth"),
            TestTime);
        var store = new TestDataStore(new AssetManagerDataSnapshot(
            definitions,
            [],
            [],
            [],
            [first, second]));

        var migrated = await new AcquisitionSourceMigrationService(store).MigrateAsync();

        Assert.True(migrated);
        Assert.Equal(1, store.TransactionSaveCount);
        var definition = store.Snapshot.FieldDefinitions.Single(
            field => field.Id == BuiltInFieldIds.AcquisitionSource);
        Assert.Equal(FieldType.SingleSelect, definition.Type);
        var option = Assert.Single(definition.Options);
        Assert.Equal("BOOTH", option.Label);
        Assert.All(store.Snapshot.Records, record =>
            Assert.Equal(
                option.Id,
                record.GetValue<SingleSelectionFieldValue>(BuiltInFieldIds.AcquisitionSource)?.Value));
    }

    [Fact]
    public async Task MigrateAsyncは選択型へ移行済みなら何も保存しない()
    {
        var store = new TestDataStore(new AssetManagerDataSnapshot(
            BuiltInFieldCatalog.All,
            [],
            [],
            [],
            []));

        var migrated = await new AcquisitionSourceMigrationService(store).MigrateAsync();

        Assert.False(migrated);
        Assert.Equal(0, store.TransactionSaveCount);
    }

    private static FieldDefinition CreateLegacyDefinition()
    {
        var current = BuiltInFieldCatalog.All.Single(
            definition => definition.Id == BuiltInFieldIds.AcquisitionSource);
        return FieldDefinition.CreateBuiltIn(
            current.Id,
            current.Label,
            FieldType.Text,
            current.SystemRole!.Value,
            current.MainTableVisible,
            current.MainTableRequired,
            current.DetailVisible,
            current.UserCanHide);
    }
}
