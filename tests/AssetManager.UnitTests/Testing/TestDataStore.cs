using AssetManager.Application.Data;
using AssetManager.Application.History;
using AssetManager.Domain.Catalog;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;

namespace AssetManager.UnitTests.Testing;

internal sealed class TestDataStore(AssetManagerDataSnapshot snapshot) : IAssetManagerDataStore
{
    public AssetManagerDataSnapshot Snapshot { get; private set; } = snapshot;

    public int TransactionSaveCount { get; private set; }

    public Task<AssetManagerDataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Snapshot);
    }

    public Task SaveRecordAsync(AssetRecord record, CancellationToken cancellationToken = default)
    {
        Snapshot = Snapshot with
        {
            Records = Snapshot.Records
                .Where(item => item.Id != record.Id)
                .Append(record)
                .ToArray(),
        };
        return Task.CompletedTask;
    }

    public Task DeleteRecordAsync(RecordId recordId, CancellationToken cancellationToken = default)
    {
        Snapshot = Snapshot with
        {
            Records = Snapshot.Records.Where(record => record.Id != recordId).ToArray(),
        };
        return Task.CompletedTask;
    }

    public Task SaveFieldDefinitionsAsync(
        IReadOnlyList<FieldDefinition> definitions,
        CancellationToken cancellationToken = default)
    {
        Snapshot = Snapshot with { FieldDefinitions = definitions };
        return Task.CompletedTask;
    }

    public Task SaveFieldsAndRecordsAsync(
        IReadOnlyList<FieldDefinition> originalDefinitions,
        IReadOnlyList<FieldDefinition> updatedDefinitions,
        IReadOnlyList<AssetRecord> updatedRecords,
        CancellationToken cancellationToken = default)
    {
        TransactionSaveCount++;
        Snapshot = Snapshot with
        {
            FieldDefinitions = updatedDefinitions,
            Records = updatedRecords,
        };
        return Task.CompletedTask;
    }

    public Task SaveAssetTypesAsync(
        IReadOnlyList<AssetTypeDefinition> assetTypes,
        CancellationToken cancellationToken = default)
    {
        Snapshot = Snapshot with { AssetTypes = assetTypes };
        return Task.CompletedTask;
    }

    public Task SaveTagsAsync(
        IReadOnlyList<TagCategoryDefinition> categories,
        IReadOnlyList<TagDefinition> tags,
        CancellationToken cancellationToken = default)
    {
        Snapshot = Snapshot with { TagCategories = categories, Tags = tags };
        return Task.CompletedTask;
    }

    public Task ApplyDataChangeAsync(
        UndoableDataChange change,
        bool useAfter,
        CancellationToken cancellationToken = default)
    {
        TransactionSaveCount++;
        var fields = (useAfter ? change.FieldsAfter : change.FieldsBefore)
            ?? Snapshot.FieldDefinitions;
        var records = Snapshot.Records.ToDictionary(record => record.Id);
        foreach (var recordChange in change.RecordChanges)
        {
            var record = useAfter ? recordChange.After : recordChange.Before;
            if (record is null)
            {
                _ = records.Remove(recordChange.RecordId);
            }
            else
            {
                records[recordChange.RecordId] = record;
            }
        }

        Snapshot = Snapshot with
        {
            FieldDefinitions = fields,
            Records = records.Values.ToArray(),
        };
        return Task.CompletedTask;
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
