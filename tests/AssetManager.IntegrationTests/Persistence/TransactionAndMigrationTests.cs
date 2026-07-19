using System.Text.Json;
using System.Text.Json.Nodes;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Migrations;
using AssetManager.Infrastructure.Persistence.Models;
using AssetManager.Infrastructure.Persistence.Transactions;

namespace AssetManager.IntegrationTests.Persistence;

public sealed class TransactionAndMigrationTests
{
    private sealed record TestDocument(int SchemaVersion, string Value);

    [Fact]
    public async Task TransactionCommitsAllFiles()
    {
        using var temporary = new TemporaryDirectory();
        var layout = new DataRootLayout(temporary.Path);
        layout.EnsureDirectories();
        var store = new AtomicJsonFileStore();
        await store.SaveAsync(layout.SettingsFile, new TestDocument(1, "old-settings"));
        await store.SaveAsync(layout.ViewsFile, new TestDocument(1, "old-views"));
        var coordinator = new JsonTransactionCoordinator(store);

        _ = await coordinator.ExecuteAsync(
            layout,
            [
                JsonFileChange.Create("settings.json", new TestDocument(1, "new-settings")),
                JsonFileChange.Create("views.json", new TestDocument(1, "new-views")),
            ]);

        Assert.Equal("new-settings", (await store.ReadAsync<TestDocument>(layout.SettingsFile)).Value);
        Assert.Equal("new-views", (await store.ReadAsync<TestDocument>(layout.ViewsFile)).Value);
        Assert.Empty(Directory.EnumerateDirectories(layout.TransactionsDirectory));
    }

    [Fact]
    public async Task TransactionRollsBackEveryFileWhenCommitFails()
    {
        using var temporary = new TemporaryDirectory();
        var layout = new DataRootLayout(temporary.Path);
        layout.EnsureDirectories();
        var store = new AtomicJsonFileStore();
        await store.SaveAsync(layout.SettingsFile, new TestDocument(1, "old-settings"));
        await store.SaveAsync(layout.ViewsFile, new TestDocument(1, "old-views"));
        var coordinator = new JsonTransactionCoordinator(store, new FailingSecondCommitter());

        await Assert.ThrowsAsync<IOException>(() => coordinator.ExecuteAsync(
            layout,
            [
                JsonFileChange.Create("settings.json", new TestDocument(1, "new-settings")),
                JsonFileChange.Create("views.json", new TestDocument(1, "new-views")),
            ]));

        Assert.Equal("old-settings", (await store.ReadAsync<TestDocument>(layout.SettingsFile)).Value);
        Assert.Equal("old-views", (await store.ReadAsync<TestDocument>(layout.ViewsFile)).Value);
        Assert.Empty(Directory.EnumerateDirectories(layout.TransactionsDirectory));
    }

    [Fact]
    public async Task StartupRecoveryRollsBackApplyingTransaction()
    {
        using var temporary = new TemporaryDirectory();
        var layout = new DataRootLayout(temporary.Path);
        layout.EnsureDirectories();
        var store = new AtomicJsonFileStore();
        await store.SaveAsync(layout.SettingsFile, new TestDocument(1, "new"));
        var transactionId = Guid.CreateVersion7().ToString("D");
        var transactionRoot = Path.Combine(layout.TransactionsDirectory, transactionId);
        var rollbackRoot = Path.Combine(transactionRoot, "rollback");
        _ = Directory.CreateDirectory(rollbackRoot);
        await store.SaveAsync(
            Path.Combine(rollbackRoot, "settings.json"),
            new TestDocument(1, "old"));
        await store.SaveAsync(
            Path.Combine(transactionRoot, "transaction.json"),
            new TransactionDocument(
                1,
                transactionId,
                TransactionState.Applying,
                [new TransactionEntryDocument("settings.json", true)]));
        var coordinator = new JsonTransactionCoordinator(store);

        await coordinator.RecoverPendingAsync(layout);

        Assert.Equal("old", (await store.ReadAsync<TestDocument>(layout.SettingsFile)).Value);
        Assert.False(Directory.Exists(transactionRoot));
    }

    [Fact]
    public async Task DatasetMigrationUpdatesManifestLastThroughTransaction()
    {
        using var temporary = new TemporaryDirectory();
        var layout = new DataRootLayout(temporary.Path);
        layout.EnsureDirectories();
        await File.WriteAllTextAsync(layout.SettingsFile, "{\"schemaVersion\":0,\"value\":\"settings\"}");
        await File.WriteAllTextAsync(layout.ManifestFile, "{\"schemaVersion\":0,\"value\":\"manifest\"}");
        var store = new AtomicJsonFileStore();
        var pipeline = new JsonMigrationPipeline([new VersionZeroToOneMigration()]);
        var service = new DatasetMigrationService(
            pipeline,
            new JsonTransactionCoordinator(store));

        var migrated = await service.MigrateAsync(layout);

        Assert.True(migrated);
        Assert.Equal(1, await ReadSchemaVersionAsync(layout.SettingsFile));
        Assert.Equal(1, await ReadSchemaVersionAsync(layout.ManifestFile));
    }

    [Fact]
    public void MigrationRejectsNewerSchemaVersion()
    {
        var pipeline = new JsonMigrationPipeline();
        var document = JsonNode.Parse("{\"schemaVersion\":2}")!.AsObject();

        Assert.Throws<UnsupportedSchemaVersionException>(() => pipeline.Migrate(document));
    }

    private static async Task<int> ReadSchemaVersionAsync(string path)
    {
        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        return root["schemaVersion"]!.GetValue<int>();
    }

    private sealed class FailingSecondCommitter : ITransactionFileCommitter
    {
        private readonly PhysicalTransactionFileCommitter _inner = new();
        private int _commitCount;

        public void Commit(string stagedPath, string targetPath)
        {
            _commitCount++;
            if (_commitCount == 2)
            {
                throw new IOException("疑似コミット失敗");
            }

            _inner.Commit(stagedPath, targetPath);
        }
    }

    private sealed class VersionZeroToOneMigration : IJsonMigrationStep
    {
        public int FromVersion => 0;

        public JsonObject Migrate(JsonObject source)
        {
            var migrated = (JsonObject)source.DeepClone();
            migrated["schemaVersion"] = 1;
            return migrated;
        }
    }
}
