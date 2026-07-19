using System.Collections.ObjectModel;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Values;

namespace AssetManager.Domain.Records;

public sealed class AssetRecord
{
    private readonly Dictionary<FieldId, FieldValue> _values;
    private readonly ReadOnlyDictionary<FieldId, FieldValue> _readOnlyValues;

    public AssetRecord(
        RecordId id,
        IEnumerable<KeyValuePair<FieldId, FieldValue>>? values,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        EnsureUtc(createdAt, nameof(createdAt));
        EnsureUtc(updatedAt, nameof(updatedAt));

        if (updatedAt < createdAt)
        {
            throw new DomainValidationException("更新日時は登録日時以降にしてください。", nameof(updatedAt));
        }

        Id = id;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        _values = values?.ToDictionary() ?? [];
        _readOnlyValues = new ReadOnlyDictionary<FieldId, FieldValue>(_values);

        if (_values.Values.Any(value => value is null))
        {
            throw new DomainValidationException("カラム値にnullは指定できません。", nameof(values));
        }
    }

    public RecordId Id { get; }

    public IReadOnlyDictionary<FieldId, FieldValue> Values => _readOnlyValues;

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public RecordStatus Status =>
        GetValue<RecordStatusFieldValue>(BuiltInFieldIds.Status)?.Value
        ?? RecordStatus.Unchecked;

    public FavoriteStatus Favorite => new(
        GetValue<BooleanFieldValue>(BuiltInFieldIds.Favorite)?.Value ?? false);

    public TargetPathFieldValue? TargetPath =>
        GetValue<TargetPathFieldValue>(BuiltInFieldIds.TargetPath);

    public static AssetRecord Create(DateTimeOffset now)
    {
        return new AssetRecord(RecordId.New(), null, now, now);
    }

    public T? GetValue<T>(FieldId id)
        where T : FieldValue
    {
        return _values.TryGetValue(id, out var value) ? value as T : null;
    }

    public AssetRecord SetValue(
        FieldDefinition definition,
        FieldValue value,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(value);

        if (definition.Type != value.Type)
        {
            throw new DomainValidationException(
                $"カラム'{definition.Label}'には{definition.Type}型の値が必要です。",
                nameof(value));
        }

        var values = new Dictionary<FieldId, FieldValue>(_values)
        {
            [definition.Id] = value,
        };

        return new AssetRecord(Id, values, CreatedAt, updatedAt);
    }

    public AssetRecord RemoveValue(FieldId id, DateTimeOffset updatedAt)
    {
        var values = new Dictionary<FieldId, FieldValue>(_values);
        _ = values.Remove(id);
        return new AssetRecord(Id, values, CreatedAt, updatedAt);
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainValidationException("日時はUTCで指定してください。", parameterName);
        }
    }
}
