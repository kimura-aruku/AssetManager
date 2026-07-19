using System.Text;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Recovery;

namespace AssetManager.IntegrationTests.Persistence;

public sealed class AtomicJsonFileStoreTests
{
    private sealed record TestDocument(int SchemaVersion, string Name, DateTimeOffset UpdatedAt);

    [Fact]
    public async Task SaveUsesUtf8WithoutBomCamelCaseAndUtcZ()
    {
        using var temporary = new TemporaryDirectory();
        var store = new AtomicJsonFileStore();
        var path = temporary.GetPath("data.json");
        var document = new TestDocument(
            1,
            "テスト",
            new DateTimeOffset(2026, 7, 19, 1, 0, 0, TimeSpan.Zero));

        await store.SaveAsync(path, document);

        var bytes = await File.ReadAllBytesAsync(path);
        var json = Encoding.UTF8.GetString(bytes);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Contains("\"schemaVersion\"", json, StringComparison.Ordinal);
        Assert.Contains("2026-07-19T01:00:00Z", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedValidationPreservesExistingFile()
    {
        using var temporary = new TemporaryDirectory();
        var store = new AtomicJsonFileStore();
        var path = temporary.GetPath("data.json");
        var original = new TestDocument(1, "元", DateTimeOffset.UnixEpoch);
        await store.SaveAsync(path, original);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(
            path,
            original with { Name = "更新" },
            _ => throw new InvalidOperationException("検証失敗")));

        var loaded = await store.ReadAsync<TestDocument>(path);
        Assert.Equal("元", loaded.Name);
        Assert.Empty(Directory.EnumerateFiles(temporary.Path, "*.rollback"));
        Assert.Empty(Directory.EnumerateFiles(temporary.Path, "*.tmp"));
    }

    [Fact]
    public async Task RecoveryRestoresValidRollbackWhenTargetIsInvalid()
    {
        using var temporary = new TemporaryDirectory();
        var store = new AtomicJsonFileStore();
        var target = temporary.GetPath("data.json");
        var rollback = AtomicJsonFileStore.GetRollbackPath(target);
        await File.WriteAllTextAsync(target, "{ broken", Encoding.UTF8);
        await store.SaveAsync(
            rollback,
            new TestDocument(1, "復元", DateTimeOffset.UnixEpoch));

        var result = await new AtomicFileRecoveryService(store).RecoverAsync(temporary.Path);

        var restored = await store.ReadAsync<TestDocument>(target);
        Assert.Equal("復元", restored.Name);
        Assert.Equal(1, result.RestoredFiles);
        Assert.False(File.Exists(rollback));
    }
}
