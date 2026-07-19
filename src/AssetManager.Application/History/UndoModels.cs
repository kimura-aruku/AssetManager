using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;

namespace AssetManager.Application.History;

public sealed record RecordStateChange(
    RecordId RecordId,
    AssetRecord? Before,
    AssetRecord? After)
{
    public RecordStateChange Validate()
    {
        if ((Before is null && After is null)
            || (Before is not null && Before.Id != RecordId)
            || (After is not null && After.Id != RecordId))
        {
            throw new ArgumentException("レコード変更の更新前後情報が正しくありません。");
        }

        return this;
    }
}

public sealed record UndoableDataChange
{
    public UndoableDataChange(
        string description,
        IEnumerable<RecordStateChange>? recordChanges = null,
        IReadOnlyList<FieldDefinition>? fieldsBefore = null,
        IReadOnlyList<FieldDefinition>? fieldsAfter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        var records = recordChanges?.Select(change => change.Validate()).ToArray() ?? [];
        if (records.Select(change => change.RecordId).Distinct().Count() != records.Length)
        {
            throw new ArgumentException("同じレコードを1操作で複数回変更できません。", nameof(recordChanges));
        }

        if ((fieldsBefore is null) != (fieldsAfter is null)
            || (records.Length == 0 && fieldsBefore is null))
        {
            throw new ArgumentException("履歴には1件以上の更新前後情報が必要です。");
        }

        Description = description;
        RecordChanges = records;
        FieldsBefore = fieldsBefore?.ToArray();
        FieldsAfter = fieldsAfter?.ToArray();
    }

    public string Description { get; }

    public IReadOnlyList<RecordStateChange> RecordChanges { get; }

    public IReadOnlyList<FieldDefinition>? FieldsBefore { get; }

    public IReadOnlyList<FieldDefinition>? FieldsAfter { get; }
}

public sealed record UndoRedoState(
    bool CanUndo,
    bool CanRedo,
    string? UndoDescription,
    string? RedoDescription);

public sealed class HistoryConstraintException(string message) : InvalidOperationException(message);

public interface IUndoHistoryPersistence
{
    Guid SessionId { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SaveEntryAsync(
        int index,
        UndoableDataChange change,
        CancellationToken cancellationToken = default);

    Task DeleteEntriesFromAsync(
        int startIndex,
        CancellationToken cancellationToken = default);

    Task SaveCursorAsync(int cursor, CancellationToken cancellationToken = default);

    Task DeleteSessionAsync(CancellationToken cancellationToken = default);
}
