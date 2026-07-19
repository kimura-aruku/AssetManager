using AssetManager.Application.Data;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Values;

namespace AssetManager.Application.Catalog;

public sealed class AcquisitionSourceMigrationService(IAssetManagerDataStore store)
{
    private readonly IAssetManagerDataStore _store = store
        ?? throw new ArgumentNullException(nameof(store));

    public async Task<bool> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var current = snapshot.FieldDefinitions.FirstOrDefault(
            definition => definition.Id == BuiltInFieldIds.AcquisitionSource)
            ?? throw new KeyNotFoundException("購入／入手元カラムが見つかりません。");
        if (current.Type == FieldType.SingleSelect)
        {
            return false;
        }

        if (current.Type != FieldType.Text || current.SystemRole is null)
        {
            throw new DomainValidationException("購入／入手元カラムを選択型へ移行できません。");
        }

        var legacyValues = snapshot.Records
            .Select(record => record.GetValue<TextFieldValue>(BuiltInFieldIds.AcquisitionSource)?.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var options = legacyValues.Select(value => new SelectionOption(
            new SelectionOptionId($"acquisition-source.{Guid.CreateVersion7():D}"),
            value)).ToArray();
        var optionByName = options.ToDictionary(
            option => option.Label,
            StringComparer.OrdinalIgnoreCase);
        var target = FieldDefinition.CreateBuiltIn(
            current.Id,
            current.Label,
            FieldType.SingleSelect,
            current.SystemRole.Value,
            current.MainTableVisible,
            current.MainTableRequired,
            current.DetailVisible,
            current.UserCanHide,
            options);
        var updatedDefinitions = snapshot.FieldDefinitions
            .Select(definition => definition.Id == target.Id ? target : definition)
            .ToArray();
        var updatedRecords = snapshot.Records.Select(record =>
        {
            var legacy = record.GetValue<TextFieldValue>(BuiltInFieldIds.AcquisitionSource);
            return legacy is null
                ? record
                : record.SetValue(
                    target,
                    new SingleSelectionFieldValue(optionByName[legacy.Value.Trim()].Id),
                    record.UpdatedAt);
        }).ToArray();

        await _store.SaveFieldsAndRecordsAsync(
            snapshot.FieldDefinitions,
            updatedDefinitions,
            updatedRecords,
            cancellationToken).ConfigureAwait(false);
        return true;
    }
}
