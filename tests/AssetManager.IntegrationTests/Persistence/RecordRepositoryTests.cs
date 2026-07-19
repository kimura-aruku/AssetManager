using System.Text.Json;
using System.Text.Json.Nodes;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Repositories;

namespace AssetManager.IntegrationTests.Persistence;

public sealed class RecordRepositoryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 19, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordRoundTripPreservesTypedAndUnknownValuesAndOmitsDefaults()
    {
        using var temporary = new TemporaryDirectory();
        var layout = new DataRootLayout(temporary.Path);
        layout.EnsureDirectories();
        var repository = new RecordRepository(new AtomicJsonFileStore());
        var unknownId = FieldId.From(CustomFieldId.New());
        var unknownElement = JsonSerializer.SerializeToElement(
            new { Future = "保持" },
            JsonDefaults.Options);
        var record = AssetRecord.Create(Now)
            .SetValue(GetDefinition(BuiltInFieldIds.Name), new TextFieldValue("素材名"), Now.AddMinutes(1))
            .SetValue(GetDefinition(BuiltInFieldIds.Favorite), new BooleanFieldValue(false), Now.AddMinutes(2))
            .SetValue(
                GetDefinition(BuiltInFieldIds.TargetPath),
                new TargetPathFieldValue(TargetPathKind.Folder, "C:\\SampleAssets"),
                Now.AddMinutes(3));
        var persisted = new PersistedAssetRecord(
            record,
            [new KeyValuePair<FieldId, JsonElement>(unknownId, unknownElement)]);

        await repository.SaveAsync(layout, persisted);

        var path = RecordRepository.GetRecordPath(layout, record.Id);
        var json = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain(BuiltInFieldIds.Favorite.Value, json, StringComparison.Ordinal);
        Assert.Contains(unknownId.Value, json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"folder\"", json, StringComparison.Ordinal);
        var loaded = await repository.LoadAsync(layout, path, BuiltInFieldCatalog.All);
        Assert.Equal("素材名", loaded.Record.Record.GetValue<TextFieldValue>(BuiltInFieldIds.Name)?.Value);
        Assert.Equal("C:\\SampleAssets", loaded.Record.Record.TargetPath?.Path);
        Assert.True(loaded.Record.UnknownValues.ContainsKey(unknownId));
        Assert.Empty(loaded.Repairs);
    }

    [Fact]
    public async Task InvalidKnownValuesAreRemovedAndReported()
    {
        using var temporary = new TemporaryDirectory();
        var layout = new DataRootLayout(temporary.Path);
        layout.EnsureDirectories();
        var repository = new RecordRepository(new AtomicJsonFileStore());
        var record = AssetRecord.Create(Now);
        await repository.SaveAsync(layout, new PersistedAssetRecord(record));
        var path = RecordRepository.GetRecordPath(layout, record.Id);
        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        var values = root["values"]!.AsObject();
        values[BuiltInFieldIds.AcquiredDate.Value] = "invalid-date";
        values[BuiltInFieldIds.Favorite.Value] = false;
        await File.WriteAllTextAsync(path, root.ToJsonString(JsonDefaults.Options));

        var loaded = await repository.LoadAsync(layout, path, BuiltInFieldCatalog.All);

        Assert.Equal(2, loaded.Repairs.Count);
        Assert.Null(loaded.Record.Record.GetValue<DateFieldValue>(BuiltInFieldIds.AcquiredDate));
        var repairedJson = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain(BuiltInFieldIds.AcquiredDate.Value, repairedJson, StringComparison.Ordinal);
        Assert.DoesNotContain(BuiltInFieldIds.Favorite.Value, repairedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CorruptRecordIsExcludedWithoutChangingSourceFile()
    {
        using var temporary = new TemporaryDirectory();
        var layout = new DataRootLayout(temporary.Path);
        layout.EnsureDirectories();
        var path = Path.Combine(layout.RecordsDirectory, "broken.json");
        const string brokenJson = "{ broken";
        await File.WriteAllTextAsync(path, brokenJson);
        var repository = new RecordRepository(new AtomicJsonFileStore());

        var result = await repository.LoadAllAsync(layout, BuiltInFieldCatalog.All);

        Assert.Empty(result.Records);
        Assert.Single(result.Failures);
        Assert.Equal(brokenJson, await File.ReadAllTextAsync(path));
    }

    private static FieldDefinition GetDefinition(FieldId id)
    {
        return BuiltInFieldCatalog.All.Single(definition => definition.Id == id);
    }
}
