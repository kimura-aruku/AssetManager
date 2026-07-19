using AssetManager.Application.Records;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Values;
using AssetManager.IntegrationTests.Persistence;
using AssetManager.Infrastructure.Operations;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Repositories;
using AssetManager.Infrastructure.Startup;

namespace AssetManager.IntegrationTests.Settings;

public sealed class DataRootMigrationServiceTests
{
    [Fact]
    public async Task ChangeAsyncは全件コピー検証後にbootstrapを切り替える()
    {
        using var temporary = new TemporaryDirectory();
        var (appPaths, source) = await CreateSourceAsync(temporary);
        var destination = temporary.GetPath("migrated-data");
        _ = Directory.CreateDirectory(destination);
        var service = new DataRootMigrationService(appPaths);

        var result = await service.ChangeAsync(source.RootDirectory, destination);

        Assert.Equal(1, result.RecordCount);
        Assert.True(File.Exists(new DataRootLayout(destination).ManifestFile));
        Assert.Single((await new JsonAssetManagerDataStore(new DataRootLayout(destination)).LoadAsync()).Records);
        var bootstrap = await new BootstrapRepository(new AtomicJsonFileStore()).LoadOrCreateAsync(appPaths);
        Assert.Equal(Path.GetFullPath(destination), bootstrap.RootDirectory);
        Assert.True(Directory.Exists(source.RootDirectory));
    }

    [Fact]
    public async Task ChangeAsyncは空でない変更先を変更せず拒否する()
    {
        using var temporary = new TemporaryDirectory();
        var (appPaths, source) = await CreateSourceAsync(temporary);
        var destination = temporary.GetPath("not-empty");
        _ = Directory.CreateDirectory(destination);
        var sentinel = Path.Combine(destination, "keep.txt");
        await File.WriteAllTextAsync(sentinel, "残す");
        var service = new DataRootMigrationService(appPaths);

        _ = await Assert.ThrowsAsync<DataRootMigrationException>(() =>
            service.ChangeAsync(source.RootDirectory, destination));

        Assert.True(File.Exists(sentinel));
        var bootstrap = await new BootstrapRepository(new AtomicJsonFileStore()).LoadOrCreateAsync(appPaths);
        Assert.Equal(source.RootDirectory, bootstrap.RootDirectory);
    }

    [Fact]
    public async Task ChangeAsyncは検証失敗時に元のbootstrapを維持する()
    {
        using var temporary = new TemporaryDirectory();
        var (appPaths, source) = await CreateSourceAsync(temporary);
        await File.WriteAllTextAsync(source.SettingsFile, "{ broken json");
        var destination = temporary.GetPath("failed-copy");
        _ = Directory.CreateDirectory(destination);
        var service = new DataRootMigrationService(appPaths);

        _ = await Assert.ThrowsAsync<DataRootMigrationException>(() =>
            service.ChangeAsync(source.RootDirectory, destination));

        Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
        var bootstrap = await new BootstrapRepository(new AtomicJsonFileStore()).LoadOrCreateAsync(appPaths);
        Assert.Equal(source.RootDirectory, bootstrap.RootDirectory);
    }

    [Fact]
    public async Task DeleteSourceAsyncは元の管理データだけを削除し登録素材を残す()
    {
        using var temporary = new TemporaryDirectory();
        var assetFile = temporary.GetPath("actual-assets", "image.png");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(assetFile)!);
        await File.WriteAllTextAsync(assetFile, "actual asset content");
        var (appPaths, source) = await CreateSourceAsync(temporary, assetFile);
        var destination = temporary.GetPath("new-data-root");
        _ = Directory.CreateDirectory(destination);
        var service = new DataRootMigrationService(appPaths);
        var migration = await service.ChangeAsync(source.RootDirectory, destination);

        await service.DeleteSourceAsync(migration);

        Assert.False(Directory.Exists(source.RootDirectory));
        Assert.True(File.Exists(assetFile));
        Assert.Equal("actual asset content", await File.ReadAllTextAsync(assetFile));
        Assert.True(Directory.Exists(destination));
    }

    private static async Task<(AppDataPaths AppPaths, DataRootLayout Source)> CreateSourceAsync(
        TemporaryDirectory temporary,
        string? targetPath = null)
    {
        var appPaths = new AppDataPaths(temporary.Path);
        var result = await new DataSetInitializer(appPaths).InitializeAsync();
        var source = new DataRootLayout(result.DataRoot);
        var records = new RecordApplicationService(new JsonAssetManagerDataStore(source));
        var values = new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("移行テスト素材"),
        };
        if (targetPath is not null)
        {
            values[BuiltInFieldIds.TargetPath] = new TargetPathFieldValue(TargetPathKind.File, targetPath);
        }

        _ = await records.CreateAsync(values);
        return (appPaths, source);
    }
}
