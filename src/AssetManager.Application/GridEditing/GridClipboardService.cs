using System.Globalization;
using AssetManager.Application.Data;
using AssetManager.Application.Records;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.Application.GridEditing;

public sealed class GridClipboardException(string message) : InvalidOperationException(message);

public sealed class GridClipboardService(
    IAssetManagerDataStore store,
    RecordApplicationService records)
{
    private readonly IAssetManagerDataStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly RecordApplicationService _records = records ?? throw new ArgumentNullException(nameof(records));

    public async Task<string> CopyAsync(
        IReadOnlyList<RecordId> recordIds,
        IReadOnlyList<FieldId> fieldIds,
        CancellationToken cancellationToken = default)
    {
        EnsureSelection(recordIds, fieldIds);
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var recordMap = snapshot.Records.ToDictionary(record => record.Id);
        var definitionMap = snapshot.FieldDefinitions.ToDictionary(field => field.Id);
        var rows = recordIds.Select(recordId =>
        {
            if (!recordMap.TryGetValue(recordId, out var record))
            {
                throw new GridClipboardException($"レコード'{recordId}'が見つかりません。");
            }

            return string.Join('\t', fieldIds.Select(fieldId =>
            {
                if (!definitionMap.TryGetValue(fieldId, out var definition))
                {
                    throw new GridClipboardException($"カラム'{fieldId}'が見つかりません。");
                }

                return FormatCell(record.Values.GetValueOrDefault(fieldId), definition, snapshot);
            }));
        });
        return string.Join(Environment.NewLine, rows);
    }

    public async Task<IReadOnlyList<AssetRecord>> PasteAsync(
        string clipboardText,
        IReadOnlyList<RecordId> targetRecordIds,
        IReadOnlyList<FieldId> targetFieldIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clipboardText);
        EnsureSelection(targetRecordIds, targetFieldIds);
        if (targetFieldIds.Contains(BuiltInFieldIds.TargetPath))
        {
            throw new GridClipboardException("対象パスには貼り付けできません。ファイルまたはフォルダー選択を使用してください。");
        }

        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var definitionMap = snapshot.FieldDefinitions.ToDictionary(field => field.Id);
        var cells = ParseClipboard(clipboardText);
        if (cells[0].Length != targetFieldIds.Count || cells.Any(row => row.Length != cells[0].Length))
        {
            throw new GridClipboardException("コピー元と貼り付け先のカラム数が一致しません。");
        }

        if (cells.Length != 1 && cells.Length != targetRecordIds.Count)
        {
            throw new GridClipboardException("コピー元と貼り付け先の行数が一致しません。1行だけのコピーは複数行へ繰り返せます。");
        }

        var updates = new Dictionary<RecordId, IReadOnlyDictionary<FieldId, FieldValue?>>(targetRecordIds.Count);
        for (var rowIndex = 0; rowIndex < targetRecordIds.Count; rowIndex++)
        {
            var sourceRow = cells.Length == 1 ? cells[0] : cells[rowIndex];
            var changes = new Dictionary<FieldId, FieldValue?>(targetFieldIds.Count);
            for (var columnIndex = 0; columnIndex < targetFieldIds.Count; columnIndex++)
            {
                var fieldId = targetFieldIds[columnIndex];
                if (!definitionMap.TryGetValue(fieldId, out var definition))
                {
                    throw new GridClipboardException($"カラム'{fieldId}'が見つかりません。");
                }

                try
                {
                    changes[fieldId] = ParseCell(sourceRow[columnIndex], definition, snapshot);
                }
                catch (Exception exception) when (exception is DomainValidationException
                                                   or FormatException
                                                   or OverflowException
                                                   or ArgumentException)
                {
                    throw new GridClipboardException(
                        $"{rowIndex + 1}行{columnIndex + 1}列（{definition.Label}）の値が正しくありません: {exception.Message}");
                }
            }

            updates[targetRecordIds[rowIndex]] = changes;
        }

        return await _records.UpdateManyAsync(
            updates,
            "セル範囲を貼り付け",
            cancellationToken).ConfigureAwait(false);
    }

    private static string[][] ParseClipboard(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        normalized = normalized.TrimEnd('\n');
        if (normalized.Length == 0)
        {
            throw new GridClipboardException("クリップボードが空です。");
        }

        return normalized.Split('\n').Select(row => row.Split('\t')).ToArray();
    }

    private static FieldValue? ParseCell(
        string cell,
        FieldDefinition definition,
        AssetManagerDataSnapshot snapshot)
    {
        var value = Unescape(cell).Trim();
        if (value.Length == 0)
        {
            return null;
        }

        return definition.Type switch
        {
            FieldType.Text => new TextFieldValue(value),
            FieldType.MultilineText => new MultilineTextFieldValue(Unescape(cell)),
            FieldType.Number => new NumberFieldValue(ParseNumber(value)),
            FieldType.Date => new DateFieldValue(ParseDate(value)),
            FieldType.Boolean => new BooleanFieldValue(ParseBoolean(value)),
            FieldType.Url => new UrlFieldValue(value),
            FieldType.SingleSelect => new SingleSelectionFieldValue(FindOption(definition, value)),
            FieldType.MultiSelect => new MultiSelectionFieldValue(
                SplitList(value).Select(item => FindOption(definition, item))),
            FieldType.FilePath => new FilePathFieldValue(value),
            FieldType.FolderPath => new FolderPathFieldValue(value),
            FieldType.TargetPath => throw new GridClipboardException("対象パスには貼り付けできません。"),
            FieldType.StringList when definition.SystemRole == SystemRole.Creators =>
                new CreatorListFieldValue(SplitList(value)),
            FieldType.StringList when definition.SystemRole == SystemRole.Sellers =>
                new SellerListFieldValue(SplitList(value)),
            FieldType.TitledPathList => new RelatedDocumentListFieldValue(
                SplitTitledList(value).Select(item => new RelatedDocument(item.Title, item.Value))),
            FieldType.TitledUrlList => new RelatedUrlListFieldValue(
                SplitTitledList(value).Select(item => new RelatedUrl(item.Title, item.Value))),
            FieldType.AssetTypeSet => new AssetTypeSetFieldValue(SplitList(value).Select(item =>
                FindAssetType(snapshot, item))),
            FieldType.TagSet => new TagSetFieldValue(SplitList(value).Select(item => FindTag(snapshot, item))),
            FieldType.RecordStatus => new RecordStatusFieldValue(ParseStatus(value)),
            _ => throw new GridClipboardException($"カラム'{definition.Label}'は貼り付けに対応していません。"),
        };
    }

    private static string FormatCell(
        FieldValue? value,
        FieldDefinition definition,
        AssetManagerDataSnapshot snapshot)
    {
        var result = value switch
        {
            null => string.Empty,
            TextFieldValue item => item.Value,
            MultilineTextFieldValue item => item.Value,
            NumberFieldValue item => item.Value.ToString(CultureInfo.InvariantCulture),
            DateFieldValue item => item.Value.ToDisplayString(),
            BooleanFieldValue item => item.Value ? "TRUE" : "FALSE",
            UrlFieldValue item => item.Value,
            SingleSelectionFieldValue item => definition.Options
                .FirstOrDefault(option => option.Id == item.Value)?.Label ?? item.Value.Value,
            MultiSelectionFieldValue item => string.Join("; ", item.Values.Select(id =>
                definition.Options.FirstOrDefault(option => option.Id == id)?.Label ?? id.Value)),
            FilePathFieldValue item => item.Value,
            FolderPathFieldValue item => item.Value,
            TargetPathFieldValue item => item.Path,
            CreatorListFieldValue item => string.Join("; ", item.Values),
            SellerListFieldValue item => string.Join("; ", item.Values),
            RelatedDocumentListFieldValue item => string.Join(";; ", item.Values.Select(entry =>
                $"{entry.Title} | {entry.Path}")),
            RelatedUrlListFieldValue item => string.Join(";; ", item.Values.Select(entry =>
                $"{entry.Title} | {entry.Url}")),
            AssetTypeSetFieldValue item => string.Join("; ", item.Values.Select(id =>
                snapshot.AssetTypes.FirstOrDefault(type => type.Id == id)?.Name ?? id.Value)),
            TagSetFieldValue item => string.Join("; ", item.Values.Select(id =>
                snapshot.Tags.FirstOrDefault(tag => tag.Id == id)?.Name ?? id.Value)),
            RecordStatusFieldValue item => item.Value.ToString(),
            _ => throw new GridClipboardException($"カラム'{definition.Label}'をコピーできません。"),
        };
        return Escape(result);
    }

    private static decimal ParseNumber(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var result)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
        {
            return result;
        }

        throw new FormatException("数値を入力してください。");
    }

    private static AssetDate ParseDate(string value)
    {
        try
        {
            return AssetDate.ParseDisplay(value);
        }
        catch (DomainValidationException)
        {
            return AssetDate.ParseStorage(value);
        }
    }

    private static bool ParseBoolean(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "TRUE" or "1" or "ON" or "はい" or "有" => true,
            "FALSE" or "0" or "OFF" or "いいえ" or "無" => false,
            _ => throw new FormatException("TRUEまたはFALSEを入力してください。"),
        };
    }

    private static RecordStatus ParseStatus(string value)
    {
        if (Enum.TryParse<RecordStatus>(value, true, out var status))
        {
            return status;
        }

        return value switch
        {
            "未確認" => RecordStatus.Unchecked,
            "利用可能" => RecordStatus.Available,
            "利用不可" => RecordStatus.Unavailable,
            "アーカイブ" => RecordStatus.Archived,
            _ => throw new FormatException("状態名が正しくありません。"),
        };
    }

    private static SelectionOptionId FindOption(FieldDefinition definition, string value)
    {
        return definition.Options.FirstOrDefault(option =>
                string.Equals(option.Id.Value, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.Label, value, StringComparison.OrdinalIgnoreCase))?.Id
            ?? throw new FormatException($"選択肢'{value}'が見つかりません。");
    }

    private static AssetTypeId FindAssetType(AssetManagerDataSnapshot snapshot, string value)
    {
        return snapshot.AssetTypes.FirstOrDefault(type =>
                string.Equals(type.Id.Value, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(type.Name, value, StringComparison.OrdinalIgnoreCase))?.Id
            ?? throw new FormatException($"種類'{value}'が見つかりません。");
    }

    private static TagId FindTag(AssetManagerDataSnapshot snapshot, string value)
    {
        return snapshot.Tags.FirstOrDefault(tag =>
                string.Equals(tag.Id.Value, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(tag.Name, value, StringComparison.OrdinalIgnoreCase))?.Id
            ?? throw new FormatException($"タグ'{value}'が見つかりません。");
    }

    private static string[] SplitList(string value)
    {
        return value.Split([';', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IEnumerable<(string Title, string Value)> SplitTitledList(string value)
    {
        foreach (var item in value.Split(";;", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = item.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
            {
                throw new FormatException("『タイトル | 値』の形式で入力してください。");
            }

            yield return (parts[0], parts[1]);
        }
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static string Unescape(string value)
    {
        var result = new System.Text.StringBuilder(value.Length);
        var escaping = false;
        foreach (var character in value)
        {
            if (!escaping && character == '\\')
            {
                escaping = true;
                continue;
            }

            if (escaping)
            {
                result.Append(character switch
                {
                    'n' => '\n',
                    't' => '\t',
                    _ => character,
                });
                escaping = false;
            }
            else
            {
                result.Append(character);
            }
        }

        if (escaping)
        {
            result.Append('\\');
        }

        return result.ToString();
    }

    private static void EnsureSelection(
        IReadOnlyList<RecordId> recordIds,
        IReadOnlyList<FieldId> fieldIds)
    {
        ArgumentNullException.ThrowIfNull(recordIds);
        ArgumentNullException.ThrowIfNull(fieldIds);
        if (recordIds.Count == 0 || fieldIds.Count == 0)
        {
            throw new GridClipboardException("コピーまたは貼り付けるセル範囲を選択してください。");
        }

        if (recordIds.Distinct().Count() != recordIds.Count || fieldIds.Distinct().Count() != fieldIds.Count)
        {
            throw new GridClipboardException("選択範囲に重複があります。");
        }
    }
}
