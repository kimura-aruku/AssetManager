using System.Text.Json;
using AssetManager.Application.Fields;
using AssetManager.Application.Catalog;
using AssetManager.Application.Records;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Licensing;
using AssetManager.Domain.Values;
using AssetManager.IntegrationTests.Persistence;
using AssetManager.Infrastructure.Operations;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Repositories;
using AssetManager.Infrastructure.Startup;

namespace AssetManager.IntegrationTests.Operations;

public sealed class JsonAssetManagerDataStoreTests
{
    [Fact]
    public async Task RecordServiceCreatesUpdatesLoadsAndDeletesJsonRecord()
    {
        using var temporary = new TemporaryDirectory();
        var (layout, store) = await CreateStoreAsync(temporary);
        var service = new RecordApplicationService(store);

        var created = await service.CreateAsync(new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("素材"),
        });
        var path = RecordRepository.GetRecordPath(layout, created.Id);
        Assert.True(File.Exists(path));

        _ = await service.UpdateAsync(created.Id, new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("更新後"),
        });
        var reloaded = Assert.Single(await new RecordApplicationService(
            new JsonAssetManagerDataStore(layout)).GetAllAsync());
        Assert.Equal("更新後", reloaded.GetValue<TextFieldValue>(BuiltInFieldIds.Name)!.Value);

        await service.DeleteAsync(created.Id);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task SavingKnownValuesPreservesUnknownJsonValues()
    {
        using var temporary = new TemporaryDirectory();
        var (layout, store) = await CreateStoreAsync(temporary);
        var unknownId = FieldId.From(CustomFieldId.New());
        var record = AssetRecord.Create(DateTimeOffset.UtcNow);
        var repository = new RecordRepository(new AtomicJsonFileStore());
        await repository.SaveAsync(
            layout,
            new PersistedAssetRecord(record, new Dictionary<FieldId, JsonElement>
            {
                [unknownId] = JsonSerializer.SerializeToElement(new { future = true }),
            }));
        var service = new RecordApplicationService(store);

        _ = await service.UpdateAsync(record.Id, new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("更新"),
        });

        await using var stream = File.OpenRead(RecordRepository.GetRecordPath(layout, record.Id));
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.True(document.RootElement.GetProperty("values").TryGetProperty(unknownId.Value, out _));
    }

    [Fact]
    public async Task FieldTypeChangePersistsDefinitionAndRecordsInTransaction()
    {
        using var temporary = new TemporaryDirectory();
        var (layout, store) = await CreateStoreAsync(temporary);
        var fields = new FieldApplicationService(store);
        var records = new RecordApplicationService(store);
        var field = await fields.AddCustomAsync("数値", FieldType.Text);
        var record = await records.CreateAsync(new Dictionary<FieldId, FieldValue?>
        {
            [field.Id] = new TextFieldValue("123.5"),
        });

        var status = await fields.ChangeTypeAsync(field.Id, FieldType.Number, false);

        Assert.Equal(FieldTypeChangeStatus.Applied, status);
        var reloaded = await new JsonAssetManagerDataStore(layout).LoadAsync();
        Assert.Equal(FieldType.Number, reloaded.FieldDefinitions.Single(item => item.Id == field.Id).Type);
        var value = reloaded.Records.Single(item => item.Id == record.Id).Values[field.Id];
        Assert.Equal(123.5m, Assert.IsType<NumberFieldValue>(value).Value);
        Assert.Empty(Directory.EnumerateDirectories(layout.TransactionsDirectory));
    }

    [Fact]
    public async Task LicensePresetAndFieldOptionArePersistedTogether()
    {
        using var temporary = new TemporaryDirectory();
        var (layout, store) = await CreateStoreAsync(temporary);
        var service = new CatalogApplicationService(store);

        var preset = await service.AddLicensePresetAsync(
            "Standard License",
            new LicenseTerms(CommercialUseAllowed: true, ModificationAllowed: true));

        var reloaded = await new JsonAssetManagerDataStore(layout).LoadAsync();
        var savedPreset = Assert.Single(reloaded.LicensePresets);
        var option = Assert.Single(reloaded.FieldDefinitions.Single(
            field => field.Id == BuiltInFieldIds.LicensePreset).Options);
        Assert.Equal(preset.Id, savedPreset.Id);
        Assert.Equal(preset.Id.Value, option.Id.Value);
        Assert.Equal(preset.Name, option.Label);
        Assert.Empty(Directory.EnumerateDirectories(layout.TransactionsDirectory));
    }

    private static async Task<(DataRootLayout Layout, JsonAssetManagerDataStore Store)> CreateStoreAsync(
        TemporaryDirectory temporary)
    {
        var result = await new DataSetInitializer(new AppDataPaths(temporary.Path)).InitializeAsync();
        var layout = new DataRootLayout(result.DataRoot);
        return (layout, new JsonAssetManagerDataStore(layout));
    }
}
