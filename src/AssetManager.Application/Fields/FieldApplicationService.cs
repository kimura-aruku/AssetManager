using System.Globalization;
using AssetManager.Application.Data;
using AssetManager.Application.History;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.Application.Fields;

public sealed record FieldTypeChangeAnalysis(
    bool CanConvertAllValues,
    IReadOnlyList<RecordId> IncompatibleRecordIds);

public enum FieldTypeChangeStatus
{
    Applied,
    RequiresValueClearConfirmation,
}

public sealed class FieldApplicationService
{
    private readonly IAssetManagerDataStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly UndoRedoService? _history;

    public FieldApplicationService(
        IAssetManagerDataStore store,
        TimeProvider? timeProvider = null,
        UndoRedoService? history = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _history = history;
    }

    public async Task<FieldDefinition> AddCustomAsync(
        string label,
        FieldType type,
        bool mainTableVisible = false,
        bool detailVisible = true,
        IEnumerable<SelectionOption>? options = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var definition = FieldDefinition.CreateCustom(
            CustomFieldId.New(),
            label,
            type,
            mainTableVisible,
            detailVisible,
            options);
        var updated = snapshot.FieldDefinitions.Append(definition).ToArray();
        await _store.SaveFieldDefinitionsAsync(updated, cancellationToken).ConfigureAwait(false);
        return definition;
    }

    public Task<FieldDefinition> RenameAsync(
        FieldId id,
        string label,
        CancellationToken cancellationToken = default)
    {
        return UpdateDefinitionAsync(id, definition => definition.Rename(label), cancellationToken);
    }

    public Task<FieldDefinition> SetVisibilityAsync(
        FieldId id,
        bool mainTableVisible,
        bool detailVisible,
        CancellationToken cancellationToken = default)
    {
        return UpdateDefinitionAsync(
            id,
            definition => definition.SetVisibility(mainTableVisible, detailVisible),
            cancellationToken);
    }

    public async Task<FieldTypeChangeAnalysis> AnalyzeTypeChangeAsync(
        FieldId id,
        FieldType newType,
        IEnumerable<SelectionOption>? options = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var current = GetDefinition(snapshot.FieldDefinitions, id);
        var target = current.ChangeType(newType, options);
        var incompatible = snapshot.Records
            .Where(record => record.Values.TryGetValue(id, out var value)
                && !TryConvert(value, target, out _))
            .Select(record => record.Id)
            .ToArray();
        return new FieldTypeChangeAnalysis(incompatible.Length == 0, incompatible);
    }

    public async Task<FieldTypeChangeStatus> ChangeTypeAsync(
        FieldId id,
        FieldType newType,
        bool clearAllValuesIfConversionFails,
        IEnumerable<SelectionOption>? options = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var current = GetDefinition(snapshot.FieldDefinitions, id);
        var target = current.ChangeType(newType, options);
        var conversions = snapshot.Records.Select(record =>
        {
            if (!record.Values.TryGetValue(id, out var value))
            {
                return new FieldValueConversion(record, false, true, null);
            }

            var canConvert = TryConvert(value, target, out var converted);
            return new FieldValueConversion(record, true, canConvert, converted);
        }).ToArray();
        var requiresClear = conversions.Any(item => item.HasValue && !item.CanConvert);
        if (requiresClear && !clearAllValuesIfConversionFails)
        {
            return FieldTypeChangeStatus.RequiresValueClearConfirmation;
        }

        var now = _timeProvider.GetUtcNow();
        var records = conversions.Select(item =>
        {
            if (!item.HasValue)
            {
                return item.Record;
            }

            if (requiresClear)
            {
                return item.Record.RemoveValue(id, now);
            }

            return item.Record.SetValue(target, item.Converted!, now);
        }).ToArray();
        var definitions = ReplaceDefinition(snapshot.FieldDefinitions, target);
        await _store.SaveFieldsAndRecordsAsync(
            snapshot.FieldDefinitions,
            definitions,
            records,
            cancellationToken).ConfigureAwait(false);
        return FieldTypeChangeStatus.Applied;
    }

    public async Task DeleteCustomAsync(
        FieldId id,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var definition = GetDefinition(snapshot.FieldDefinitions, id);
        if (!definition.UserCanDelete)
        {
            throw new DomainValidationException("このカラムは削除できません。", nameof(id));
        }

        var now = _timeProvider.GetUtcNow();
        var definitions = snapshot.FieldDefinitions.Where(item => item.Id != id).ToArray();
        var recordChanges = snapshot.Records
            .Where(record => record.Values.ContainsKey(id))
            .Select(record => new RecordStateChange(
                record.Id,
                record,
                record.RemoveValue(id, now)))
            .ToArray();
        var change = new UndoableDataChange(
            "カスタムカラムを削除",
            recordChanges,
            snapshot.FieldDefinitions,
            definitions);
        if (_history is null)
        {
            await _store.ApplyDataChangeAsync(
                change,
                useAfter: true,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _history.ExecuteAsync(change, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<FieldDefinition> UpdateDefinitionAsync(
        FieldId id,
        Func<FieldDefinition, FieldDefinition> update,
        CancellationToken cancellationToken)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var updated = update(GetDefinition(snapshot.FieldDefinitions, id));
        await _store.SaveFieldDefinitionsAsync(
            ReplaceDefinition(snapshot.FieldDefinitions, updated),
            cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private static FieldDefinition GetDefinition(
        IReadOnlyList<FieldDefinition> definitions,
        FieldId id)
    {
        return definitions.FirstOrDefault(definition => definition.Id == id)
            ?? throw new KeyNotFoundException($"カラム'{id}'が見つかりません。");
    }

    private static FieldDefinition[] ReplaceDefinition(
        IReadOnlyList<FieldDefinition> definitions,
        FieldDefinition updated)
    {
        return definitions.Select(item => item.Id == updated.Id ? updated : item).ToArray();
    }

    private static bool TryConvert(
        FieldValue value,
        FieldDefinition target,
        out FieldValue? converted)
    {
        try
        {
            converted = ConvertValue(value, target);
            return converted is not null;
        }
        catch (Exception exception) when (exception is DomainValidationException or FormatException or OverflowException)
        {
            converted = null;
            return false;
        }
    }

    private static FieldValue? ConvertValue(FieldValue value, FieldDefinition target)
    {
        if (value.Type == target.Type)
        {
            return IsValidSelection(value, target) ? value : null;
        }

        var text = GetText(value);
        return target.Type switch
        {
            FieldType.Text when text is not null => new TextFieldValue(text),
            FieldType.MultilineText when text is not null => new MultilineTextFieldValue(text),
            FieldType.Number when text is not null => new NumberFieldValue(
                decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture)),
            FieldType.Date when text is not null => new DateFieldValue(ParseDate(text)),
            FieldType.Boolean when text is not null => new BooleanFieldValue(bool.Parse(text)),
            FieldType.Url when text is not null => new UrlFieldValue(text),
            FieldType.FilePath when text is not null => new FilePathFieldValue(text),
            FieldType.FolderPath when text is not null => new FolderPathFieldValue(text),
            FieldType.SingleSelect when value is MultiSelectionFieldValue { Values.Count: 1 } multiple
                && target.Options.Any(option => option.Id == multiple.Values[0]) =>
                new SingleSelectionFieldValue(multiple.Values[0]),
            FieldType.MultiSelect when value is SingleSelectionFieldValue single
                && target.Options.Any(option => option.Id == single.Value) =>
                new MultiSelectionFieldValue([single.Value]),
            _ => null,
        };
    }

    private static string? GetText(FieldValue value)
    {
        return value switch
        {
            TextFieldValue item => item.Value,
            MultilineTextFieldValue item => item.Value,
            NumberFieldValue item => item.Value.ToString(CultureInfo.InvariantCulture),
            DateFieldValue item => item.Value.ToStorageString(),
            BooleanFieldValue item => item.Value.ToString(CultureInfo.InvariantCulture),
            UrlFieldValue item => item.Value,
            FilePathFieldValue item => item.Value,
            FolderPathFieldValue item => item.Value,
            SingleSelectionFieldValue item => item.Value.Value,
            _ => null,
        };
    }

    private static AssetDate ParseDate(string value)
    {
        try
        {
            return AssetDate.ParseStorage(value);
        }
        catch (DomainValidationException)
        {
            return AssetDate.ParseDisplay(value);
        }
    }

    private static bool IsValidSelection(FieldValue value, FieldDefinition target)
    {
        var optionIds = target.Options.Select(option => option.Id).ToHashSet();
        return value switch
        {
            SingleSelectionFieldValue item => optionIds.Contains(item.Value),
            MultiSelectionFieldValue item => item.Values.All(optionIds.Contains),
            _ => true,
        };
    }

    private sealed record FieldValueConversion(
        AssetRecord Record,
        bool HasValue,
        bool CanConvert,
        FieldValue? Converted);
}
