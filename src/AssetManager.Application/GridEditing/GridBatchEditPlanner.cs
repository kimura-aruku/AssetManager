using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Values;

namespace AssetManager.Application.GridEditing;

public static class GridBatchEditPlanner
{
    public static IReadOnlyDictionary<RecordId, IReadOnlyDictionary<FieldId, FieldValue?>> CreateUniformUpdates(
        IReadOnlyList<RecordId> recordIds,
        IReadOnlyDictionary<FieldId, FieldValue?> editedFields)
    {
        ArgumentNullException.ThrowIfNull(recordIds);
        ArgumentNullException.ThrowIfNull(editedFields);
        if (recordIds.Count == 0)
        {
            throw new GridClipboardException("一括編集するレコードを選択してください。");
        }

        if (editedFields.Count == 0)
        {
            throw new GridClipboardException("変更されたカラムはありません。");
        }

        if (recordIds.Distinct().Count() != recordIds.Count)
        {
            throw new GridClipboardException("一括編集するレコードが重複しています。");
        }

        if (recordIds.Count > 1 && editedFields.ContainsKey(BuiltInFieldIds.TargetPath))
        {
            throw new GridClipboardException("対象パスは複数レコードへ一括適用できません。");
        }

        return recordIds.ToDictionary(
            id => id,
            _ => (IReadOnlyDictionary<FieldId, FieldValue?>)new Dictionary<FieldId, FieldValue?>(editedFields));
    }
}
