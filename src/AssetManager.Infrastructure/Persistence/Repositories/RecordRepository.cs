using System.Collections.ObjectModel;
using System.Text.Json;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Models;

namespace AssetManager.Infrastructure.Persistence.Repositories;

public sealed record PersistedAssetRecord
{
    private readonly ReadOnlyDictionary<FieldId, JsonElement> _unknownValues;

    public PersistedAssetRecord(
        AssetRecord record,
        IEnumerable<KeyValuePair<FieldId, JsonElement>>? unknownValues = null)
    {
        Record = record ?? throw new ArgumentNullException(nameof(record));
        var copied = unknownValues?.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone()) ?? [];
        _unknownValues = new ReadOnlyDictionary<FieldId, JsonElement>(copied);
    }

    public AssetRecord Record { get; }

    public IReadOnlyDictionary<FieldId, JsonElement> UnknownValues => _unknownValues;
}

public sealed record RecordFieldRepair(
    RecordId RecordId,
    string FieldId,
    string Reason,
    string OriginalContent);

public sealed record RecordReadFailure(string Path, string Error);

public sealed record RecordLoadItem(
    PersistedAssetRecord Record,
    IReadOnlyList<RecordFieldRepair> Repairs);

public sealed record RecordLoadBatchResult(
    IReadOnlyList<PersistedAssetRecord> Records,
    IReadOnlyList<RecordFieldRepair> Repairs,
    IReadOnlyList<RecordReadFailure> Failures);

public sealed class RecordRepository(AtomicJsonFileStore store)
{
    public async Task SaveAsync(
        DataRootLayout layout,
        PersistedAssetRecord persistedRecord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(persistedRecord);
        var path = GetRecordPath(layout, persistedRecord.Record.Id);
        var document = CreateDocument(persistedRecord);
        await store.SaveAsync(path, document, ValidateDocument, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RecordLoadItem> LoadAsync(
        DataRootLayout layout,
        string path,
        IEnumerable<FieldDefinition> definitions,
        CancellationToken cancellationToken = default)
    {
        var definitionMap = definitions.ToDictionary(definition => definition.Id);
        var document = await store.ReadAsync<RecordDocument>(
            path,
            ValidateDocument,
            cancellationToken).ConfigureAwait(false);
        var recordId = RecordId.Parse(document.Id);
        var expectedFileName = $"{recordId}.json";
        if (!string.Equals(Path.GetFileName(path), expectedFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new DataPersistenceException("レコードIDとファイル名が一致していません。");
        }

        var values = new Dictionary<FieldId, FieldValue>();
        var unknownValues = new Dictionary<FieldId, JsonElement>();
        var repairs = new List<RecordFieldRepair>();

        foreach (var (rawFieldId, element) in document.Values)
        {
            FieldId fieldId;
            try
            {
                fieldId = new FieldId(rawFieldId);
            }
            catch (Exception exception) when (exception is DomainValidationException or FormatException)
            {
                repairs.Add(new RecordFieldRepair(
                    recordId,
                    rawFieldId,
                    exception.Message,
                    element.GetRawText()));
                continue;
            }

            if (!definitionMap.TryGetValue(fieldId, out var definition))
            {
                unknownValues[fieldId] = element.Clone();
                continue;
            }

            if (IsOmittedDefault(definition.Type, element))
            {
                repairs.Add(new RecordFieldRepair(
                    recordId,
                    rawFieldId,
                    "既定値または空配列はJSONから省略されます。",
                    element.GetRawText()));
                continue;
            }

            try
            {
                values[fieldId] = ParseValue(definition, element);
            }
            catch (Exception exception) when (exception is JsonException or DomainValidationException or FormatException or InvalidOperationException)
            {
                repairs.Add(new RecordFieldRepair(
                    recordId,
                    rawFieldId,
                    exception.Message,
                    element.GetRawText()));
            }
        }

        var record = new AssetRecord(
            recordId,
            values,
            document.CreatedAt,
            document.UpdatedAt);
        var persisted = new PersistedAssetRecord(record, unknownValues);

        if (repairs.Count > 0)
        {
            await SaveAsync(layout, persisted, cancellationToken).ConfigureAwait(false);
        }

        return new RecordLoadItem(persisted, repairs.AsReadOnly());
    }

    public async Task<RecordLoadBatchResult> LoadAllAsync(
        DataRootLayout layout,
        IEnumerable<FieldDefinition> definitions,
        CancellationToken cancellationToken = default)
    {
        var records = new List<PersistedAssetRecord>();
        var repairs = new List<RecordFieldRepair>();
        var failures = new List<RecordReadFailure>();

        if (!Directory.Exists(layout.RecordsDirectory))
        {
            return new RecordLoadBatchResult(records, repairs, failures);
        }

        foreach (var path in Directory.EnumerateFiles(layout.RecordsDirectory, "*.json").Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var item = await LoadAsync(layout, path, definitions, cancellationToken).ConfigureAwait(false);
                records.Add(item.Record);
                repairs.AddRange(item.Repairs);
            }
            catch (Exception exception) when (exception is IOException
                                               or UnauthorizedAccessException
                                               or JsonException
                                               or DataPersistenceException
                                               or DomainValidationException
                                               or FormatException
                                               or ArgumentException)
            {
                failures.Add(new RecordReadFailure(path, exception.Message));
            }
        }

        return new RecordLoadBatchResult(
            records.AsReadOnly(),
            repairs.AsReadOnly(),
            failures.AsReadOnly());
    }

    public static string GetRecordPath(DataRootLayout layout, RecordId id)
    {
        return Path.Combine(layout.RecordsDirectory, $"{id}.json");
    }

    public static RecordDocument CreateDocument(PersistedAssetRecord persisted)
    {
        var values = persisted.UnknownValues.ToDictionary(
            pair => pair.Key.Value,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);

        foreach (var (fieldId, value) in persisted.Record.Values)
        {
            if (ShouldPersist(value))
            {
                values[fieldId.Value] = SerializeValue(value);
            }
            else
            {
                _ = values.Remove(fieldId.Value);
            }
        }

        return new RecordDocument(
            JsonDefaults.CurrentSchemaVersion,
            persisted.Record.Id.ToString(),
            values,
            persisted.Record.CreatedAt,
            persisted.Record.UpdatedAt);
    }

    private static void ValidateDocument(RecordDocument document)
    {
        SchemaVersionGuard.EnsureCurrent(document.SchemaVersion);
        _ = RecordId.Parse(document.Id);
        ArgumentNullException.ThrowIfNull(document.Values);

        if (document.CreatedAt.Offset != TimeSpan.Zero
            || document.UpdatedAt.Offset != TimeSpan.Zero
            || document.UpdatedAt < document.CreatedAt)
        {
            throw new DataPersistenceException("レコードの登録日時または更新日時が正しくありません。");
        }
    }

    private static FieldValue ParseValue(FieldDefinition definition, JsonElement element)
    {
        return definition.Type switch
        {
            FieldType.Text => new TextFieldValue(GetString(element)),
            FieldType.MultilineText => new MultilineTextFieldValue(GetString(element)),
            FieldType.Number => new NumberFieldValue(element.GetDecimal()),
            FieldType.Date => new DateFieldValue(AssetDate.ParseStorage(GetString(element))),
            FieldType.Boolean => new BooleanFieldValue(element.GetBoolean()),
            FieldType.Url => new UrlFieldValue(GetString(element)),
            FieldType.SingleSelect => new SingleSelectionFieldValue(new SelectionOptionId(GetString(element))),
            FieldType.MultiSelect => new MultiSelectionFieldValue(
                GetStringArray(element).Select(value => new SelectionOptionId(value))),
            FieldType.FilePath => new FilePathFieldValue(GetString(element)),
            FieldType.FolderPath => new FolderPathFieldValue(GetString(element)),
            FieldType.TargetPath => ParseTargetPath(element),
            FieldType.StringList => ParseStringList(definition, element),
            FieldType.TitledPathList => ParseRelatedDocuments(element),
            FieldType.TitledUrlList => ParseRelatedUrls(element),
            FieldType.AssetTypeSet => new AssetTypeSetFieldValue(
                GetStringArray(element).Select(value => new AssetTypeId(value))),
            FieldType.TagSet => new TagSetFieldValue(
                GetStringArray(element).Select(value => new TagId(value))),
            FieldType.RecordStatus => new RecordStatusFieldValue(
                element.Deserialize<RecordStatus>(JsonDefaults.Options)),
            _ => throw new JsonException($"未対応のカラム型です: {definition.Type}"),
        };
    }

    private static JsonElement SerializeValue(FieldValue value)
    {
        object serializable = value switch
        {
            TextFieldValue item => item.Value,
            MultilineTextFieldValue item => item.Value,
            NumberFieldValue item => item.Value,
            DateFieldValue item => item.Value.ToStorageString(),
            BooleanFieldValue item => item.Value,
            UrlFieldValue item => item.Value,
            SingleSelectionFieldValue item => item.Value.Value,
            MultiSelectionFieldValue item => item.Values.Select(id => id.Value).ToArray(),
            FilePathFieldValue item => item.Value,
            FolderPathFieldValue item => item.Value,
            TargetPathFieldValue item => new
            {
                Kind = item.Kind,
                Path = item.Path,
            },
            AssetTypeSetFieldValue item => item.Values.Select(id => id.Value).ToArray(),
            TagSetFieldValue item => item.Values.Select(id => id.Value).ToArray(),
            RecordStatusFieldValue item => item.Value,
            CreatorListFieldValue item => item.Values,
            SellerListFieldValue item => item.Values,
            RelatedDocumentListFieldValue item => item.Values.Select(document => new
            {
                document.Title,
                document.Path,
            }).ToArray(),
            RelatedUrlListFieldValue item => item.Values.Select(url => new
            {
                url.Title,
                url.Url,
            }).ToArray(),
            _ => throw new JsonException($"未対応のカラム値型です: {value.GetType().Name}"),
        };

        return JsonSerializer.SerializeToElement(serializable, JsonDefaults.Options);
    }

    private static bool ShouldPersist(FieldValue value)
    {
        return value switch
        {
            BooleanFieldValue item => item.Value,
            MultiSelectionFieldValue item => item.Values.Count > 0,
            AssetTypeSetFieldValue item => item.Values.Count > 0,
            TagSetFieldValue item => item.Values.Count > 0,
            CreatorListFieldValue item => item.Values.Count > 0,
            SellerListFieldValue item => item.Values.Count > 0,
            RelatedDocumentListFieldValue item => item.Values.Count > 0,
            RelatedUrlListFieldValue item => item.Values.Count > 0,
            _ => true,
        };
    }

    private static bool IsOmittedDefault(FieldType type, JsonElement element)
    {
        return (type == FieldType.Boolean && element.ValueKind == JsonValueKind.False)
            || (type is FieldType.MultiSelect
                    or FieldType.AssetTypeSet
                    or FieldType.TagSet
                    or FieldType.StringList
                    or FieldType.TitledPathList
                    or FieldType.TitledUrlList
                && element.ValueKind == JsonValueKind.Array
                && element.GetArrayLength() == 0);
    }

    private static string GetString(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("文字列値が必要です。");
        }

        return element.GetString() ?? throw new JsonException("null文字列は使用できません。");
    }

    private static string[] GetStringArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("配列値が必要です。");
        }

        return element.EnumerateArray().Select(GetString).ToArray();
    }

    private static TargetPathFieldValue ParseTargetPath(JsonElement element)
    {
        var kindText = GetString(element.GetProperty("kind"));
        var kind = kindText switch
        {
            "file" => TargetPathKind.File,
            "folder" => TargetPathKind.Folder,
            _ => throw new JsonException("対象パスのkindはfileまたはfolderにしてください。"),
        };
        return new TargetPathFieldValue(kind, GetString(element.GetProperty("path")));
    }

    private static FieldValue ParseStringList(FieldDefinition definition, JsonElement element)
    {
        var values = GetStringArray(element);
        return definition.SystemRole switch
        {
            SystemRole.Creators => new CreatorListFieldValue(values),
            SystemRole.Sellers => new SellerListFieldValue(values),
            _ => throw new JsonException("stringList型には制作者または販売者のsystemRoleが必要です。"),
        };
    }

    private static RelatedDocumentListFieldValue ParseRelatedDocuments(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("関連文書には配列値が必要です。");
        }

        return new RelatedDocumentListFieldValue(element.EnumerateArray().Select(item => new RelatedDocument(
            GetString(item.GetProperty("title")),
            GetString(item.GetProperty("path")))));
    }

    private static RelatedUrlListFieldValue ParseRelatedUrls(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("関連URLには配列値が必要です。");
        }

        return new RelatedUrlListFieldValue(element.EnumerateArray().Select(item => new RelatedUrl(
            GetString(item.GetProperty("title")),
            GetString(item.GetProperty("url")))));
    }
}
