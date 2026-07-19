using System.Text.Json.Nodes;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Transactions;

namespace AssetManager.Infrastructure.Persistence.Migrations;

public interface IJsonMigrationStep
{
    int FromVersion { get; }

    JsonObject Migrate(JsonObject source);
}

public sealed class JsonMigrationPipeline
{
    private readonly Dictionary<int, IJsonMigrationStep> _steps;

    public JsonMigrationPipeline(IEnumerable<IJsonMigrationStep>? steps = null)
    {
        var copied = steps?.ToArray() ?? [];
        if (copied.Select(step => step.FromVersion).Distinct().Count() != copied.Length)
        {
            throw new ArgumentException("同じ移行元バージョンを複数登録できません。", nameof(steps));
        }

        _steps = copied.ToDictionary(step => step.FromVersion);
    }

    public JsonObject Migrate(JsonObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var migrated = (JsonObject)source.DeepClone();
        var version = ReadSchemaVersion(migrated);

        if (version > JsonDefaults.CurrentSchemaVersion)
        {
            throw new UnsupportedSchemaVersionException(version, JsonDefaults.CurrentSchemaVersion);
        }

        while (version < JsonDefaults.CurrentSchemaVersion)
        {
            if (!_steps.TryGetValue(version, out var step))
            {
                throw new DataPersistenceException($"schemaVersion {version} の移行処理がありません。");
            }

            migrated = step.Migrate(migrated)
                ?? throw new DataPersistenceException($"schemaVersion {version} の移行結果がnullです。");
            var nextVersion = ReadSchemaVersion(migrated);
            if (nextVersion != version + 1)
            {
                throw new DataPersistenceException("移行処理はschemaVersionを1つだけ進める必要があります。");
            }

            version = nextVersion;
        }

        return migrated;
    }

    public static int ReadSchemaVersion(JsonObject document)
    {
        if (document["schemaVersion"] is not JsonValue value
            || !value.TryGetValue<int>(out var version)
            || version < 0)
        {
            throw new DataPersistenceException("schemaVersionを読み取れません。");
        }

        return version;
    }
}

public sealed class DatasetMigrationService(
    JsonMigrationPipeline pipeline,
    JsonTransactionCoordinator transactions)
{
    public async Task<bool> MigrateAsync(
        DataRootLayout layout,
        CancellationToken cancellationToken = default)
    {
        var documentPaths = EnumerateDocumentPaths(layout).ToArray();
        var changes = new List<JsonFileChange>();

        foreach (var path in documentPaths.Where(path => path != layout.ManifestFile))
        {
            JsonFileChange? change;
            try
            {
                change = await CreateChangeIfRequiredAsync(layout, path, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                IsRecordPath(layout, path)
                && exception is System.Text.Json.JsonException or DataPersistenceException)
            {
                // Individual record failures are reported and excluded by RecordRepository.
                // Core documents remain critical and still abort startup.
                continue;
            }

            if (change is not null)
            {
                changes.Add(change);
            }
        }

        var manifestChange = await CreateChangeIfRequiredAsync(
            layout,
            layout.ManifestFile,
            cancellationToken).ConfigureAwait(false);
        if (manifestChange is not null)
        {
            changes.Add(manifestChange);
        }

        if (changes.Count == 0)
        {
            return false;
        }

        _ = await transactions.ExecuteAsync(layout, changes, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static bool IsRecordPath(DataRootLayout layout, string path)
    {
        return string.Equals(
            Path.GetDirectoryName(Path.GetFullPath(path)),
            Path.GetFullPath(layout.RecordsDirectory),
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<JsonFileChange?> CreateChangeIfRequiredAsync(
        DataRootLayout layout,
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var source = JsonNode.Parse(json) as JsonObject
            ?? throw new DataPersistenceException($"JSONオブジェクトを読み込めません: {path}");
        var version = JsonMigrationPipeline.ReadSchemaVersion(source);

        if (version > JsonDefaults.CurrentSchemaVersion)
        {
            throw new UnsupportedSchemaVersionException(version, JsonDefaults.CurrentSchemaVersion);
        }

        if (version == JsonDefaults.CurrentSchemaVersion)
        {
            return null;
        }

        var migrated = pipeline.Migrate(source);
        var relativePath = Path.GetRelativePath(layout.RootDirectory, path);
        return JsonFileChange.Create(relativePath, migrated);
    }

    private static IEnumerable<string> EnumerateDocumentPaths(DataRootLayout layout)
    {
        yield return layout.FieldsFile;
        yield return layout.AssetTypesFile;
        yield return layout.TagsFile;
        yield return layout.SettingsFile;
        yield return layout.ViewsFile;

        if (Directory.Exists(layout.RecordsDirectory))
        {
            foreach (var recordPath in Directory.EnumerateFiles(layout.RecordsDirectory, "*.json"))
            {
                yield return recordPath;
            }
        }

        yield return layout.ManifestFile;
    }
}
