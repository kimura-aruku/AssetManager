using System.Text.Json;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Models;

namespace AssetManager.Infrastructure.Persistence.Repositories;

public sealed class BootstrapRepository(AtomicJsonFileStore store)
{
    public async Task<DataRootLayout> LoadOrCreateAsync(
        AppDataPaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        paths.EnsureFixedDirectories();

        if (!File.Exists(paths.BootstrapFile))
        {
            var created = new BootstrapDocument(
                JsonDefaults.CurrentSchemaVersion,
                paths.DefaultDataRoot);
            await store.SaveAsync(
                paths.BootstrapFile,
                created,
                Validate,
                cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var document = await store.ReadAsync<BootstrapDocument>(
                paths.BootstrapFile,
                Validate,
                cancellationToken).ConfigureAwait(false);
            return new DataRootLayout(document.DataRoot);
        }
        catch (Exception exception) when (exception is IOException or JsonException or DataPersistenceException or ArgumentException)
        {
            throw new CriticalDataFileException(paths.BootstrapFile, exception);
        }
    }

    public Task SaveAsync(
        AppDataPaths paths,
        string dataRoot,
        CancellationToken cancellationToken = default)
    {
        var document = new BootstrapDocument(
            JsonDefaults.CurrentSchemaVersion,
            Path.GetFullPath(dataRoot));
        return store.SaveAsync(paths.BootstrapFile, document, Validate, cancellationToken);
    }

    private static void Validate(BootstrapDocument document)
    {
        SchemaVersionGuard.EnsureCurrent(document.SchemaVersion);
        if (string.IsNullOrWhiteSpace(document.DataRoot) || !Path.IsPathFullyQualified(document.DataRoot))
        {
            throw new DataPersistenceException("bootstrap.jsonのdataRootは絶対パスで指定してください。");
        }
    }
}

public sealed class ManifestRepository(AtomicJsonFileStore store)
{
    public async Task<ManifestDocument> LoadAsync(
        DataRootLayout layout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await store.ReadAsync<ManifestDocument>(
                layout.ManifestFile,
                Validate,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or JsonException or DataPersistenceException)
        {
            throw new CriticalDataFileException(layout.ManifestFile, exception);
        }
    }

    public Task SaveAsync(
        DataRootLayout layout,
        ManifestDocument document,
        CancellationToken cancellationToken = default)
    {
        return store.SaveAsync(layout.ManifestFile, document, Validate, cancellationToken);
    }

    public static ManifestDocument Create(DateTimeOffset createdAt)
    {
        if (createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("登録日時はUTCで指定してください。", nameof(createdAt));
        }

        return new ManifestDocument(
            JsonDefaults.CurrentSchemaVersion,
            Guid.CreateVersion7().ToString("D"),
            createdAt);
    }

    private static void Validate(ManifestDocument document)
    {
        SchemaVersionGuard.EnsureCurrent(document.SchemaVersion);
        if (!Guid.TryParse(document.CatalogId, out var catalogId)
            || catalogId.ToString("D")[14] != '7')
        {
            throw new DataPersistenceException("manifest.jsonのcatalogIdはUUID Version 7で指定してください。");
        }

        if (document.CreatedAt.Offset != TimeSpan.Zero)
        {
            throw new DataPersistenceException("manifest.jsonのcreatedAtはUTCで指定してください。");
        }
    }
}

public sealed class SettingsRepository(AtomicJsonFileStore store)
{
    public Task<AppSettingsDocument> LoadAsync(
        DataRootLayout layout,
        CancellationToken cancellationToken = default)
    {
        return LoadCriticalAsync(layout.SettingsFile, cancellationToken);
    }

    public Task SaveAsync(
        DataRootLayout layout,
        AppSettingsDocument document,
        CancellationToken cancellationToken = default)
    {
        return store.SaveAsync(layout.SettingsFile, document, Validate, cancellationToken);
    }

    private async Task<AppSettingsDocument> LoadCriticalAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            return await store.ReadAsync<AppSettingsDocument>(
                path,
                Validate,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or JsonException or DataPersistenceException)
        {
            throw new CriticalDataFileException(path, exception);
        }
    }

    private static void Validate(AppSettingsDocument document)
    {
        SchemaVersionGuard.EnsureCurrent(document.SchemaVersion);
        if (document.LicenseWarningDays < 1)
        {
            throw new DataPersistenceException("licenseWarningDaysは1以上にしてください。");
        }
    }
}

public sealed class ViewSettingsRepository(AtomicJsonFileStore store)
{
    public async Task<ViewSettingsDocument> LoadAsync(
        DataRootLayout layout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await store.ReadAsync<ViewSettingsDocument>(
                layout.ViewsFile,
                Validate,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or JsonException or DataPersistenceException)
        {
            throw new CriticalDataFileException(layout.ViewsFile, exception);
        }
    }

    public Task SaveAsync(
        DataRootLayout layout,
        ViewSettingsDocument document,
        CancellationToken cancellationToken = default)
    {
        return store.SaveAsync(layout.ViewsFile, document, Validate, cancellationToken);
    }

    private static void Validate(ViewSettingsDocument document)
    {
        SchemaVersionGuard.EnsureCurrent(document.SchemaVersion);
        if (document.MainTableColumns.Any(column =>
                string.IsNullOrWhiteSpace(column.Id)
                || column.Order < 0
                || column.Width <= 0)
            || document.MainTableColumns.Select(column => column.Id).Distinct(StringComparer.Ordinal).Count()
            != document.MainTableColumns.Count
            || document.MainTableColumns.Select(column => column.Order).Distinct().Count()
            != document.MainTableColumns.Count)
        {
            throw new DataPersistenceException("views.jsonのカラム設定が正しくありません。");
        }

        var searches = document.SavedSearches ?? [];
        if (searches.Any(search =>
                !Guid.TryParse(search.Id, out var searchId)
                || searchId.ToString("D")[14] != '7'
                || string.IsNullOrWhiteSpace(search.Name)
                || search.Conditions.Select(condition => condition.FieldId)
                    .Distinct(StringComparer.Ordinal).Count() != search.Conditions.Count)
            || searches.Select(search => search.Id).Distinct(StringComparer.Ordinal).Count() != searches.Count)
        {
            throw new DataPersistenceException("views.jsonの保存済み検索が正しくありません。");
        }

        foreach (var condition in searches.SelectMany(search => search.Conditions))
        {
            _ = new Domain.Identifiers.FieldId(condition.FieldId);
            var valid = condition.Kind switch
            {
                "text" => !string.IsNullOrWhiteSpace(condition.Text)
                    && condition.Boolean is null
                    && condition.OptionIds is null,
                "boolean" => condition.Text is null
                    && condition.Boolean is not null
                    && condition.OptionIds is null,
                "optionAny" => condition.Text is null
                    && condition.Boolean is null
                    && condition.OptionIds is { Count: > 0 }
                    && condition.OptionIds.All(id => !string.IsNullOrWhiteSpace(id))
                    && condition.OptionIds.Distinct(StringComparer.Ordinal).Count()
                    == condition.OptionIds.Count,
                _ => false,
            };
            if (!valid)
            {
                throw new DataPersistenceException("保存済み検索条件の形式が正しくありません。");
            }
        }
    }
}
