using AssetManager.Application.Data;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.Application.Fields;

public sealed class StandardFieldMigrationService(IAssetManagerDataStore store)
{
    private readonly IAssetManagerDataStore _store = store
        ?? throw new ArgumentNullException(nameof(store));

    public async Task<bool> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var currentDetail = snapshot.FieldDefinitions.FirstOrDefault(
            definition => definition.Id == BuiltInFieldIds.Description);
        var needsMigration = snapshot.FieldDefinitions.All(
                definition => definition.Id != BuiltInFieldIds.Overview)
            || currentDetail?.Label != "詳細"
            || snapshot.FieldDefinitions.Any(definition => definition.Id == BuiltInFieldIds.Notes);
        if (!needsMigration)
        {
            return false;
        }

        var existingById = snapshot.FieldDefinitions.ToDictionary(definition => definition.Id);
        var canonicalDetail = BuiltInFieldCatalog.All.Single(
            definition => definition.Id == BuiltInFieldIds.Description);
        var updatedDefinitions = new List<FieldDefinition>();
        foreach (var canonical in BuiltInFieldCatalog.All)
        {
            if (canonical.Id == BuiltInFieldIds.Description && currentDetail is not null)
            {
                updatedDefinitions.Add(CreateRenamedDetail(currentDetail, canonicalDetail));
            }
            else
            {
                updatedDefinitions.Add(existingById.GetValueOrDefault(canonical.Id) ?? canonical);
            }
        }

        updatedDefinitions.AddRange(snapshot.FieldDefinitions.Where(
            definition => definition.Origin == FieldOrigin.Custom));
        var detailDefinition = updatedDefinitions.Single(
            definition => definition.Id == BuiltInFieldIds.Description);
        var updatedRecords = snapshot.Records
            .Select(record => MigrateRecord(record, detailDefinition))
            .ToArray();

        await _store.SaveFieldsAndRecordsAsync(
            snapshot.FieldDefinitions,
            updatedDefinitions,
            updatedRecords,
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static FieldDefinition CreateRenamedDetail(
        FieldDefinition current,
        FieldDefinition canonical)
    {
        return FieldDefinition.CreateBuiltIn(
            canonical.Id,
            canonical.Label,
            canonical.Type,
            canonical.SystemRole!.Value,
            current.MainTableVisible,
            current.MainTableRequired,
            current.DetailVisible,
            current.UserCanHide,
            current.Options);
    }

    private static AssetRecord MigrateRecord(
        AssetRecord record,
        FieldDefinition detailDefinition)
    {
        var notes = record.GetValue<MultilineTextFieldValue>(BuiltInFieldIds.Notes);
        if (notes is null)
        {
            return record.RemoveValue(BuiltInFieldIds.Notes, record.UpdatedAt);
        }

        var detail = record.GetValue<MultilineTextFieldValue>(BuiltInFieldIds.Description);
        var combined = detail is null
            ? notes.Value
            : $"{detail.Value}{Environment.NewLine}{Environment.NewLine}{notes.Value}";
        return record
            .SetValue(
                detailDefinition,
                new MultilineTextFieldValue(combined),
                record.UpdatedAt)
            .RemoveValue(BuiltInFieldIds.Notes, record.UpdatedAt);
    }
}
