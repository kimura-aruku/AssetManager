using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Migrations;
using AssetManager.Infrastructure.Persistence.Recovery;
using AssetManager.Infrastructure.Persistence.Repositories;
using AssetManager.Infrastructure.Persistence.Transactions;

namespace AssetManager.Infrastructure.Operations;

public sealed record DataRootMigrationResult(
    string SourceRoot,
    string DestinationRoot,
    int RecordCount);

public sealed class DataRootMigrationException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

public sealed class DataRootMigrationService
{
    private readonly AppDataPaths _appPaths;
    private readonly AtomicJsonFileStore _store;

    public DataRootMigrationService(AppDataPaths appPaths, AtomicJsonFileStore? store = null)
    {
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        _store = store ?? new AtomicJsonFileStore();
    }

    public async Task<DataRootMigrationResult> ChangeAsync(
        string currentRoot,
        string destinationRoot,
        CancellationToken cancellationToken = default)
    {
        var source = Path.GetFullPath(currentRoot);
        var destination = Path.GetFullPath(destinationRoot);
        ValidateRoots(source, destination);
        var destinationCreated = false;
        var destinationSafeToClean = false;
        try
        {
            if (!Directory.Exists(destination))
            {
                _ = Directory.CreateDirectory(destination);
                destinationCreated = true;
            }

            if (Directory.EnumerateFileSystemEntries(destination).Any())
            {
                throw new DataRootMigrationException("変更先には空のフォルダーを指定してください。");
            }

            destinationSafeToClean = true;

            var sourceLayout = new DataRootLayout(source);
            var destinationLayout = new DataRootLayout(destination);
            var sourceSnapshot = await CreateLoader().LoadAsync(sourceLayout, cancellationToken).ConfigureAwait(false);
            CopyDataset(sourceLayout, destinationLayout, cancellationToken);
            var copiedSnapshot = await CreateLoader().LoadAsync(destinationLayout, cancellationToken).ConfigureAwait(false);
            ValidateCopiedDataset(sourceSnapshot, copiedSnapshot);

            await new BootstrapRepository(_store)
                .SaveAsync(_appPaths, destination, cancellationToken)
                .ConfigureAwait(false);
            return new DataRootMigrationResult(
                source,
                destination,
                copiedSnapshot.RecordLoadResult.Records.Count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException
                                          and not DataRootMigrationException)
        {
            if (destinationSafeToClean)
            {
                TryCleanDestination(destination, destinationCreated);
            }
            throw new DataRootMigrationException(
                "データ保存先を変更できませんでした。元の保存先は維持されています。",
                exception);
        }
        catch
        {
            if (destinationSafeToClean)
            {
                TryCleanDestination(destination, destinationCreated);
            }
            throw;
        }
    }

    public async Task DeleteSourceAsync(
        DataRootMigrationResult migration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migration);
        cancellationToken.ThrowIfCancellationRequested();
        var bootstrap = await new BootstrapRepository(_store)
            .LoadOrCreateAsync(_appPaths, cancellationToken)
            .ConfigureAwait(false);
        if (!PathsEqual(bootstrap.RootDirectory, migration.DestinationRoot))
        {
            throw new DataRootMigrationException("変更先が現在のデータ保存先ではないため、元データを削除できません。");
        }

        var source = Path.GetFullPath(migration.SourceRoot);
        var destination = Path.GetFullPath(migration.DestinationRoot);
        if (PathsEqual(source, destination) || !Directory.Exists(source))
        {
            return;
        }

        Directory.Delete(source, recursive: true);
    }

    private static void ValidateRoots(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            throw new DataRootMigrationException("現在のデータ保存先が見つかりません。");
        }

        if (PathsEqual(source, destination))
        {
            throw new DataRootMigrationException("現在とは異なる変更先を指定してください。");
        }

        var relativeFromSource = Path.GetRelativePath(source, destination);
        var relativeFromDestination = Path.GetRelativePath(destination, source);
        if (!IsOutside(relativeFromSource) || !IsOutside(relativeFromDestination))
        {
            throw new DataRootMigrationException("現在の保存先と親子関係にあるフォルダーは指定できません。");
        }

        var root = Path.GetPathRoot(destination)
            ?? throw new DataRootMigrationException("変更先のドライブを判定できません。");
        if (new DriveInfo(root).DriveType != DriveType.Fixed)
        {
            throw new DataRootMigrationException("固定ローカルドライブ上のフォルダーを指定してください。");
        }
    }

    private static bool IsOutside(string relativePath)
    {
        return relativePath == ".."
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static void CopyDataset(
        DataRootLayout source,
        DataRootLayout destination,
        CancellationToken cancellationToken)
    {
        destination.EnsureDirectories();
        CopyFile(source.ManifestFile, destination.ManifestFile);
        CopyFile(source.SettingsFile, destination.SettingsFile);
        CopyFile(source.ViewsFile, destination.ViewsFile);
        CopyDirectory(source.DefinitionsDirectory, destination.DefinitionsDirectory, cancellationToken);
        CopyDirectory(source.RecordsDirectory, destination.RecordsDirectory, cancellationToken);
    }

    private static void CopyDirectory(string source, string destination, CancellationToken cancellationToken)
    {
        _ = Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static void CopyFile(string source, string destination)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: false);
    }

    private static void ValidateCopiedDataset(
        DataSetSnapshot source,
        DataSetSnapshot copied)
    {
        if (source.Manifest.CatalogId != copied.Manifest.CatalogId
            || source.RecordLoadResult.Records.Count != copied.RecordLoadResult.Records.Count
            || copied.RecordLoadResult.Failures.Count > 0
            || copied.RecordLoadResult.Repairs.Count > 0
            || source.Fields.Count != copied.Fields.Count
            || source.AssetTypes.Count != copied.AssetTypes.Count
            || source.LicensePresets.Count != copied.LicensePresets.Count
            || source.Tags.Tags.Count != copied.Tags.Tags.Count
            || source.Tags.Categories.Count != copied.Tags.Categories.Count)
        {
            throw new DataRootMigrationException("コピー後の全件検証に失敗しました。元の保存先は維持されています。");
        }
    }

    private DataSetLoader CreateLoader()
    {
        var transactions = new JsonTransactionCoordinator(_store);
        return new DataSetLoader(
            new AtomicFileRecoveryService(_store),
            transactions,
            new DatasetMigrationService(new JsonMigrationPipeline(), transactions),
            new ManifestRepository(_store),
            new FieldDefinitionRepository(_store),
            new AssetTypeRepository(_store),
            new LicensePresetRepository(_store),
            new TagRepository(_store),
            new SettingsRepository(_store),
            new ViewSettingsRepository(_store),
            new RecordRepository(_store));
    }

    private static void TryCleanDestination(string destination, bool destinationCreated)
    {
        try
        {
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
                if (!destinationCreated)
                {
                    _ = Directory.CreateDirectory(destination);
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            StringComparison.OrdinalIgnoreCase);
    }
}
