using AssetManager.Application.Startup;
using AssetManager.Domain.Catalog;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Migrations;
using AssetManager.Infrastructure.Persistence.Models;
using AssetManager.Infrastructure.Persistence.Recovery;
using AssetManager.Infrastructure.Persistence.Repositories;
using AssetManager.Infrastructure.Persistence.Transactions;

namespace AssetManager.Infrastructure.Startup;

public sealed class DataSetInitializer : IStartupInitializer
{
    private const int TotalSteps = 7;
    private readonly AppDataPaths _paths;
    private readonly TimeProvider _timeProvider;
    private readonly AtomicJsonFileStore _store;

    public DataSetInitializer(
        AppDataPaths paths,
        TimeProvider? timeProvider = null,
        AtomicJsonFileStore? store = null)
    {
        _paths = paths;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _store = store ?? new AtomicJsonFileStore();
    }

    public async Task<StartupResult> InitializeAsync(
        IProgress<StartupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        DataRootLayout layout;
        JsonTransactionCoordinator transactions;
        bool createdInitialData;
        try
        {
            Report(progress, "アプリ領域を準備しています。", 1);
            _paths.EnsureFixedDirectories();

            Report(progress, "データ保存先を確認しています。", 2);
            var bootstrapWasMissing = !File.Exists(_paths.BootstrapFile);
            var bootstrap = new BootstrapRepository(_store);
            layout = await bootstrap.LoadOrCreateAsync(_paths, cancellationToken).ConfigureAwait(false);
            layout.EnsureDirectories();

            transactions = new JsonTransactionCoordinator(_store);
            Report(progress, "未完了の保存処理を確認しています。", 3);
            _ = await new AtomicFileRecoveryService(_store)
                .RecoverAsync(layout.RootDirectory, cancellationToken)
                .ConfigureAwait(false);
            await transactions.RecoverPendingAsync(layout, cancellationToken).ConfigureAwait(false);

            Report(progress, "前回の編集履歴を整理しています。", 4);
            RecreateUndoDirectory(layout);

            Report(progress, "初期データを準備しています。", 5);
            createdInitialData = bootstrapWasMissing
                | await CreateMissingInitialFilesAsync(layout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new StartupException(
                StartupFailureKind.DataCreation,
                "アプリの保存領域または初期データを準備できませんでした。",
                exception);
        }

        try
        {
            Report(progress, "データ形式を確認しています。", 6);
            var migration = new DatasetMigrationService(
                new JsonMigrationPipeline(),
                transactions);
            var loader = CreateLoader(transactions, migration);

            Report(progress, "管理データを読み込んでいます。", 7);
            var snapshot = await loader.LoadAsync(layout, cancellationToken).ConfigureAwait(false);

            return new StartupResult(
                layout.RootDirectory,
                snapshot.RecordLoadResult.Records.Count,
                snapshot.RecordLoadResult.Repairs.Count,
                snapshot.RecordLoadResult.Failures.Count,
                createdInitialData,
                snapshot.Settings.CheckPathsOnStartup,
                snapshot.Settings.LicenseWarningDays);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new StartupException(
                StartupFailureKind.DataLoading,
                "管理データを読み込めませんでした。",
                exception);
        }
    }

    private async Task<bool> CreateMissingInitialFilesAsync(
        DataRootLayout layout,
        CancellationToken cancellationToken)
    {
        var created = false;
        var manifestRepository = new ManifestRepository(_store);
        if (!File.Exists(layout.ManifestFile))
        {
            await manifestRepository.SaveAsync(
                layout,
                ManifestRepository.Create(_timeProvider.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
            created = true;
        }

        if (!File.Exists(layout.FieldsFile))
        {
            await new FieldDefinitionRepository(_store)
                .SaveAsync(layout, BuiltInFieldCatalog.All, cancellationToken)
                .ConfigureAwait(false);
            created = true;
        }

        if (!File.Exists(layout.AssetTypesFile))
        {
            await new AssetTypeRepository(_store)
                .SaveAsync(layout, CreateDefaultAssetTypes(), cancellationToken)
                .ConfigureAwait(false);
            created = true;
        }

        if (!File.Exists(layout.TagsFile))
        {
            await new TagRepository(_store)
                .SaveAsync(layout, new TagCatalog([], []), cancellationToken)
                .ConfigureAwait(false);
            created = true;
        }

        if (!File.Exists(layout.SettingsFile))
        {
            await new SettingsRepository(_store)
                .SaveAsync(
                    layout,
                    new AppSettingsDocument(
                        JsonDefaults.CurrentSchemaVersion,
                        CheckPathsOnStartup: true,
                        LicenseWarningDays: 365),
                    cancellationToken)
                .ConfigureAwait(false);
            created = true;
        }

        if (!File.Exists(layout.ViewsFile))
        {
            await new ViewSettingsRepository(_store)
                .SaveAsync(layout, CreateDefaultViews(), cancellationToken)
                .ConfigureAwait(false);
            created = true;
        }

        return created;
    }

    private DataSetLoader CreateLoader(
        JsonTransactionCoordinator transactions,
        DatasetMigrationService migration)
    {
        return new DataSetLoader(
            new AtomicFileRecoveryService(_store),
            transactions,
            migration,
            new ManifestRepository(_store),
            new FieldDefinitionRepository(_store),
            new AssetTypeRepository(_store),
            new TagRepository(_store),
            new SettingsRepository(_store),
            new ViewSettingsRepository(_store),
            new RecordRepository(_store));
    }

    private static IReadOnlyList<AssetTypeDefinition> CreateDefaultAssetTypes()
    {
        return
        [
            new AssetTypeDefinition(new AssetTypeId("type.font"), "フォント", [".ttf", ".otf", ".woff", ".woff2"]),
            new AssetTypeDefinition(new AssetTypeId("type.bgm"), "BGM", [".wav", ".mp3", ".ogg", ".flac"]),
            new AssetTypeDefinition(new AssetTypeId("type.sound-effect"), "効果音", [".wav", ".mp3", ".ogg"]),
            new AssetTypeDefinition(new AssetTypeId("type.image"), "画像", [".png", ".jpg", ".jpeg", ".webp", ".gif"]),
            new AssetTypeDefinition(new AssetTypeId("type.texture"), "テクスチャー", [".png", ".jpg", ".jpeg", ".tga", ".psd"]),
            new AssetTypeDefinition(new AssetTypeId("type.ui"), "UI素材", [".png", ".svg", ".psd"]),
            new AssetTypeDefinition(new AssetTypeId("type.model-3d"), "3Dモデル", [".fbx", ".obj", ".blend", ".gltf", ".glb"]),
            new AssetTypeDefinition(new AssetTypeId("type.motion"), "モーション", [".fbx", ".bvh", ".anim"]),
        ];
    }

    private static ViewSettingsDocument CreateDefaultViews()
    {
        return new ViewSettingsDocument(
            JsonDefaults.CurrentSchemaVersion,
            [
                new ViewColumnDocument(MainTableColumns.TargetPath.Value, 0, 360, true),
                new ViewColumnDocument(MainTableColumns.Name.Value, 1, 240, true),
                new ViewColumnDocument(MainTableColumns.AssetTypes.Value, 2, 160, true),
                new ViewColumnDocument(MainTableColumns.LicenseSummary.Value, 3, 200, true),
            ]);
    }

    private static void RecreateUndoDirectory(DataRootLayout layout)
    {
        if (Directory.Exists(layout.UndoDirectory))
        {
            Directory.Delete(layout.UndoDirectory, recursive: true);
        }

        _ = Directory.CreateDirectory(layout.UndoDirectory);
    }

    private static void Report(
        IProgress<StartupProgress>? progress,
        string message,
        int completedSteps)
    {
        progress?.Report(new StartupProgress(message, completedSteps, TotalSteps));
    }
}
