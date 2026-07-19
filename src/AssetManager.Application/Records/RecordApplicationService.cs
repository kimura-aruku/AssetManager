using AssetManager.Application.Data;
using AssetManager.Domain.Catalog;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Validation;
using AssetManager.Domain.Values;

namespace AssetManager.Application.Records;

public sealed class RecordApplicationService
{
    private readonly IAssetManagerDataStore _store;
    private readonly TimeProvider _timeProvider;

    public RecordApplicationService(
        IAssetManagerDataStore store,
        TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<AssetRecord>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return (await _store.LoadAsync(cancellationToken).ConfigureAwait(false)).Records;
    }

    public async Task<AssetRecord?> GetAsync(
        RecordId id,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.Records.FirstOrDefault(record => record.Id == id);
    }

    public async Task<AssetRecord> CreateAsync(
        IReadOnlyDictionary<FieldId, FieldValue?> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var record = ApplyChanges(
            AssetRecord.Create(now),
            values,
            snapshot.FieldDefinitions,
            now);
        record = AssignTypesFromTargetFile(record, snapshot.FieldDefinitions, snapshot.AssetTypes, now);
        ValidateRecord(record, snapshot);
        await _store.SaveRecordAsync(record, cancellationToken).ConfigureAwait(false);
        return record;
    }

    public async Task<AssetRecord> UpdateAsync(
        RecordId id,
        IReadOnlyDictionary<FieldId, FieldValue?> changes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var existing = snapshot.Records.FirstOrDefault(record => record.Id == id)
            ?? throw new KeyNotFoundException($"レコード'{id}'が見つかりません。");
        var now = _timeProvider.GetUtcNow();
        var updated = ApplyChanges(existing, changes, snapshot.FieldDefinitions, now);
        updated = AssignTypesFromTargetFile(updated, snapshot.FieldDefinitions, snapshot.AssetTypes, now);
        ValidateRecord(updated, snapshot);
        await _store.SaveRecordAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task DeleteAsync(
        RecordId id,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.Records.All(record => record.Id != id))
        {
            throw new KeyNotFoundException($"レコード'{id}'が見つかりません。");
        }

        await _store.DeleteRecordAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public static bool HasMissingTargetPath(AssetRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.TargetPath is null;
    }

    private static AssetRecord ApplyChanges(
        AssetRecord record,
        IReadOnlyDictionary<FieldId, FieldValue?> changes,
        IReadOnlyList<FieldDefinition> definitions,
        DateTimeOffset updatedAt)
    {
        var definitionMap = definitions.ToDictionary(definition => definition.Id);
        var updated = record;
        foreach (var (fieldId, value) in changes)
        {
            if (!definitionMap.TryGetValue(fieldId, out var definition))
            {
                throw new DomainValidationException($"未定義のカラム'{fieldId}'は更新できません。", nameof(changes));
            }

            updated = value is null
                ? updated.RemoveValue(fieldId, updatedAt)
                : updated.SetValue(definition, value, updatedAt);
        }

        return updated;
    }

    private static AssetRecord AssignTypesFromTargetFile(
        AssetRecord record,
        IReadOnlyList<FieldDefinition> definitions,
        IReadOnlyList<AssetTypeDefinition> assetTypes,
        DateTimeOffset updatedAt)
    {
        var currentTypes = record.GetValue<AssetTypeSetFieldValue>(BuiltInFieldIds.AssetTypes);
        if (currentTypes is { Values.Count: > 0 }
            || record.TargetPath is not { Kind: TargetPathKind.File } targetPath)
        {
            return record;
        }

        var extension = Path.GetExtension(targetPath.Path);
        if (string.IsNullOrEmpty(extension))
        {
            return record;
        }

        var matches = assetTypes
            .Where(type => type.MatchesExtension(extension))
            .Select(type => type.Id)
            .ToArray();
        if (matches.Length == 0)
        {
            return record;
        }

        var definition = definitions.Single(item => item.Id == BuiltInFieldIds.AssetTypes);
        return record.SetValue(definition, new AssetTypeSetFieldValue(matches), updatedAt);
    }

    private static void ValidateRecord(
        AssetRecord record,
        AssetManagerDataSnapshot snapshot)
    {
        var result = DomainModelValidator.ValidateRecord(
            record,
            snapshot.FieldDefinitions,
            snapshot.AssetTypes,
            snapshot.Tags);
        if (!result.IsValid)
        {
            throw new DomainValidationException(
                string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        }
    }
}
