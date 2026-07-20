using AssetManager.Application.Data;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.Application.Fields;

public sealed class StandardFieldMigrationService(IAssetManagerDataStore store)
{
    private static readonly FieldId[] ObsoleteBuiltInFieldIds =
    [
        BuiltInFieldIds.LinkRequired,
        BuiltInFieldIds.LogoRequired,
        BuiltInFieldIds.AdultUseAllowed,
        BuiltInFieldIds.LicenseUnknown,
    ];

    private readonly IAssetManagerDataStore _store = store
        ?? throw new ArgumentNullException(nameof(store));

    public async Task<bool> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var existingById = snapshot.FieldDefinitions.ToDictionary(definition => definition.Id);
        var needsMigration = BuiltInFieldCatalog.All.Any(canonical =>
                !existingById.TryGetValue(canonical.Id, out var current)
                || current.Label != canonical.Label
                || current.SystemRole != canonical.SystemRole)
            || snapshot.FieldDefinitions.Any(definition =>
                definition.Id == BuiltInFieldIds.Notes
                || ObsoleteBuiltInFieldIds.Contains(definition.Id));
        if (!needsMigration)
        {
            return false;
        }

        var updatedDefinitions = new List<FieldDefinition>();
        foreach (var canonical in BuiltInFieldCatalog.All)
        {
            updatedDefinitions.Add(existingById.TryGetValue(canonical.Id, out var current)
                ? CreateCanonicalDefinition(current, canonical)
                : canonical);
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

    private static FieldDefinition CreateCanonicalDefinition(
        FieldDefinition current,
        FieldDefinition canonical)
    {
        return FieldDefinition.CreateBuiltIn(
            canonical.Id,
            canonical.Label,
            current.Type,
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
        var migrated = record;
        if (notes is not null)
        {
            var detail = record.GetValue<MultilineTextFieldValue>(BuiltInFieldIds.Description);
            var combined = detail is null
                ? notes.Value
                : $"{detail.Value}{Environment.NewLine}{Environment.NewLine}{notes.Value}";
            migrated = migrated.SetValue(
                detailDefinition,
                new MultilineTextFieldValue(combined),
                record.UpdatedAt);
        }

        migrated = migrated.RemoveValue(BuiltInFieldIds.Notes, record.UpdatedAt);
        foreach (var obsoleteId in ObsoleteBuiltInFieldIds)
        {
            migrated = migrated.RemoveValue(obsoleteId, record.UpdatedAt);
        }

        return migrated;
    }
}
