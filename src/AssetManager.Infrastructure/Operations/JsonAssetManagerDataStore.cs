using AssetManager.Application.Data;
using AssetManager.Application.History;
using AssetManager.Domain.Catalog;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Licensing;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Repositories;
using AssetManager.Infrastructure.Persistence.Transactions;

namespace AssetManager.Infrastructure.Operations;

public sealed class JsonAssetManagerDataStore : IAssetManagerDataStore
{
    private readonly DataRootLayout _layout;
    private readonly AtomicJsonFileStore _fileStore;
    private readonly FieldDefinitionRepository _fields;
    private readonly AssetTypeRepository _assetTypes;
    private readonly LicensePresetRepository _licensePresets;
    private readonly TagRepository _tags;
    private readonly RecordRepository _records;
    private readonly JsonTransactionCoordinator _transactions;

    public JsonAssetManagerDataStore(
        DataRootLayout layout,
        AtomicJsonFileStore? fileStore = null)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _fileStore = fileStore ?? new AtomicJsonFileStore();
        _fields = new FieldDefinitionRepository(_fileStore);
        _assetTypes = new AssetTypeRepository(_fileStore);
        _licensePresets = new LicensePresetRepository(_fileStore);
        _tags = new TagRepository(_fileStore);
        _records = new RecordRepository(_fileStore);
        _transactions = new JsonTransactionCoordinator(_fileStore);
    }

    public async Task<AssetManagerDataSnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var definitions = await _fields.LoadAsync(_layout, cancellationToken).ConfigureAwait(false);
        var assetTypes = await _assetTypes.LoadAsync(_layout, cancellationToken).ConfigureAwait(false);
        var licensePresets = await _licensePresets.LoadAsync(_layout, cancellationToken).ConfigureAwait(false);
        var tags = await _tags.LoadAsync(_layout, cancellationToken).ConfigureAwait(false);
        var records = await _records.LoadAllAsync(_layout, definitions, cancellationToken).ConfigureAwait(false);
        LicensePresetRepository.ValidateFieldOptions(definitions, licensePresets);
        return new AssetManagerDataSnapshot(
            definitions,
            assetTypes,
            tags.Categories,
            tags.Tags,
            records.Records.Select(item => item.Record).ToArray())
        {
            LicensePresets = licensePresets,
        };
    }

    public async Task SaveRecordAsync(
        AssetRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var definitions = await _fields.LoadAsync(_layout, cancellationToken).ConfigureAwait(false);
        var persisted = await WithUnknownValuesAsync(record, definitions, cancellationToken).ConfigureAwait(false);
        await _records.SaveAsync(_layout, persisted, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteRecordAsync(
        RecordId recordId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = RecordRepository.GetRecordPath(_layout, recordId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task SaveFieldDefinitionsAsync(
        IReadOnlyList<FieldDefinition> definitions,
        CancellationToken cancellationToken = default)
    {
        return _fields.SaveAsync(_layout, definitions, cancellationToken);
    }

    public async Task SaveFieldsAndRecordsAsync(
        IReadOnlyList<FieldDefinition> originalDefinitions,
        IReadOnlyList<FieldDefinition> updatedDefinitions,
        IReadOnlyList<AssetRecord> updatedRecords,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(originalDefinitions);
        ArgumentNullException.ThrowIfNull(updatedDefinitions);
        ArgumentNullException.ThrowIfNull(updatedRecords);
        var changes = new List<JsonFileChange>
        {
            JsonFileChange.Create(
                Path.GetRelativePath(_layout.RootDirectory, _layout.FieldsFile),
                FieldDefinitionRepository.CreateDocument(updatedDefinitions)),
        };

        foreach (var record in updatedRecords)
        {
            var persisted = await WithUnknownValuesAsync(
                record,
                originalDefinitions,
                cancellationToken).ConfigureAwait(false);
            var path = RecordRepository.GetRecordPath(_layout, record.Id);
            changes.Add(JsonFileChange.Create(
                Path.GetRelativePath(_layout.RootDirectory, path),
                RecordRepository.CreateDocument(persisted)));
        }

        _ = await _transactions.ExecuteAsync(_layout, changes, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveAssetTypesAsync(
        IReadOnlyList<AssetTypeDefinition> assetTypes,
        CancellationToken cancellationToken = default)
    {
        return _assetTypes.SaveAsync(_layout, assetTypes, cancellationToken);
    }

    public async Task SaveLicensePresetsAsync(
        IReadOnlyList<LicensePresetDefinition> licensePresets,
        IReadOnlyList<FieldDefinition> fieldDefinitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(licensePresets);
        ArgumentNullException.ThrowIfNull(fieldDefinitions);
        var changes = new[]
        {
            JsonFileChange.Create(
                Path.GetRelativePath(_layout.RootDirectory, _layout.LicensePresetsFile),
                LicensePresetRepository.CreateDocument(licensePresets)),
            JsonFileChange.Create(
                Path.GetRelativePath(_layout.RootDirectory, _layout.FieldsFile),
                FieldDefinitionRepository.CreateDocument(fieldDefinitions)),
        };
        _ = await _transactions.ExecuteAsync(_layout, changes, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveTagsAsync(
        IReadOnlyList<TagCategoryDefinition> categories,
        IReadOnlyList<TagDefinition> tags,
        CancellationToken cancellationToken = default)
    {
        return _tags.SaveAsync(_layout, new TagCatalog(categories, tags), cancellationToken);
    }

    public async Task ApplyDataChangeAsync(
        UndoableDataChange change,
        bool useAfter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        var currentDefinitions = await _fields.LoadAsync(_layout, cancellationToken).ConfigureAwait(false);
        var targetDefinitions = (useAfter ? change.FieldsAfter : change.FieldsBefore)
            ?? currentDefinitions;
        var changes = new List<JsonFileChange>();

        if (change.FieldsBefore is not null)
        {
            changes.Add(JsonFileChange.Create(
                Path.GetRelativePath(_layout.RootDirectory, _layout.FieldsFile),
                FieldDefinitionRepository.CreateDocument(targetDefinitions)));
        }

        foreach (var recordChange in change.RecordChanges)
        {
            var target = useAfter ? recordChange.After : recordChange.Before;
            var path = RecordRepository.GetRecordPath(_layout, recordChange.RecordId);
            var relativePath = Path.GetRelativePath(_layout.RootDirectory, path);
            if (target is null)
            {
                changes.Add(JsonFileChange.Delete(relativePath));
                continue;
            }

            var persisted = await WithUnknownValuesAsync(
                target,
                currentDefinitions,
                cancellationToken).ConfigureAwait(false);
            changes.Add(JsonFileChange.Create(
                relativePath,
                RecordRepository.CreateDocument(persisted)));
        }

        _ = await _transactions.ExecuteAsync(_layout, changes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PersistedAssetRecord> WithUnknownValuesAsync(
        AssetRecord record,
        IReadOnlyList<FieldDefinition> definitions,
        CancellationToken cancellationToken)
    {
        var path = RecordRepository.GetRecordPath(_layout, record.Id);
        if (!File.Exists(path))
        {
            return new PersistedAssetRecord(record);
        }

        var existing = await _records.LoadAsync(
            _layout,
            path,
            definitions,
            cancellationToken).ConfigureAwait(false);
        return new PersistedAssetRecord(record, existing.Record.UnknownValues);
    }
}
