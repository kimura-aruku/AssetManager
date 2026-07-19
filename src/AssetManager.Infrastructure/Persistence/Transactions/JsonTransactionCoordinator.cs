using System.Text.Json;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Models;

namespace AssetManager.Infrastructure.Persistence.Transactions;

public sealed record JsonFileChange(
    string RelativePath,
    JsonElement? Content,
    bool DeleteTarget = false)
{
    public static JsonFileChange Create<T>(string relativePath, T content)
    {
        return new JsonFileChange(
            relativePath,
            JsonSerializer.SerializeToElement(content, JsonDefaults.Options));
    }

    public static JsonFileChange Delete(string relativePath)
    {
        return new JsonFileChange(relativePath, null, DeleteTarget: true);
    }
}

public interface ITransactionFileCommitter
{
    void Commit(string stagedPath, string targetPath);
}

public sealed class PhysicalTransactionFileCommitter : ITransactionFileCommitter
{
    public void Commit(string stagedPath, string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new IOException("更新対象ディレクトリを解決できません。");
        _ = Directory.CreateDirectory(directory);
        File.Move(stagedPath, targetPath, overwrite: true);
    }
}

public sealed class JsonTransactionCoordinator
{
    private const string ManifestFileName = "transaction.json";
    private readonly AtomicJsonFileStore _store;
    private readonly ITransactionFileCommitter _committer;

    public JsonTransactionCoordinator(
        AtomicJsonFileStore store,
        ITransactionFileCommitter? committer = null)
    {
        _store = store;
        _committer = committer ?? new PhysicalTransactionFileCommitter();
    }

    public async Task<string> ExecuteAsync(
        DataRootLayout layout,
        IEnumerable<JsonFileChange> changes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(changes);
        var copiedChanges = changes.ToArray();
        ValidateChanges(layout, copiedChanges);

        if (copiedChanges.Length == 0)
        {
            throw new ArgumentException("1件以上の変更が必要です。", nameof(changes));
        }

        var transactionId = Guid.CreateVersion7().ToString("D");
        var transactionRoot = Path.Combine(layout.TransactionsDirectory, transactionId);
        var stagedRoot = Path.Combine(transactionRoot, "staged");
        var rollbackRoot = Path.Combine(transactionRoot, "rollback");
        var manifestPath = Path.Combine(transactionRoot, ManifestFileName);
        _ = Directory.CreateDirectory(stagedRoot);
        _ = Directory.CreateDirectory(rollbackRoot);

        var entries = copiedChanges.Select(change => new TransactionEntryDocument(
            NormalizeRelativePath(change.RelativePath),
            File.Exists(layout.ResolveRelativePath(change.RelativePath)),
            change.DeleteTarget)).ToArray();
        var state = TransactionState.Preparing;

        try
        {
            await SaveManifestAsync(manifestPath, transactionId, state, entries, cancellationToken)
                .ConfigureAwait(false);

            for (var index = 0; index < copiedChanges.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (copiedChanges[index].DeleteTarget)
                {
                    continue;
                }

                var stagedPath = ResolveTransactionPath(stagedRoot, entries[index].RelativePath);
                await _store.SaveAsync(
                    stagedPath,
                    copiedChanges[index].Content!.Value,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            foreach (var entry in entries.Where(entry => entry.ExistedBeforeTransaction))
            {
                var targetPath = layout.ResolveRelativePath(entry.RelativePath);
                var rollbackPath = ResolveTransactionPath(rollbackRoot, entry.RelativePath);
                var rollbackDirectory = Path.GetDirectoryName(rollbackPath)
                    ?? throw new IOException("ロールバック先を解決できません。");
                _ = Directory.CreateDirectory(rollbackDirectory);
                CopyAndFlush(targetPath, rollbackPath, overwrite: false);
            }

            state = TransactionState.Prepared;
            await SaveManifestAsync(manifestPath, transactionId, state, entries, cancellationToken)
                .ConfigureAwait(false);
            state = TransactionState.Applying;
            await SaveManifestAsync(manifestPath, transactionId, state, entries, cancellationToken)
                .ConfigureAwait(false);

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var targetPath = layout.ResolveRelativePath(entry.RelativePath);
                if (entry.DeleteTarget)
                {
                    if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }
                }
                else
                {
                    _committer.Commit(
                        ResolveTransactionPath(stagedRoot, entry.RelativePath),
                        targetPath);
                }
            }

            state = TransactionState.Committed;
            await SaveManifestAsync(manifestPath, transactionId, state, entries, cancellationToken)
                .ConfigureAwait(false);
            Directory.Delete(transactionRoot, recursive: true);
            return transactionId;
        }
        catch (Exception transactionException)
        {
            try
            {
                Rollback(layout, rollbackRoot, entries, state == TransactionState.Applying);
                if (Directory.Exists(transactionRoot))
                {
                    Directory.Delete(transactionRoot, recursive: true);
                }
            }
            catch (Exception rollbackException)
            {
                throw new DataPersistenceException(
                    "トランザクションとロールバックの両方に失敗しました。",
                    new AggregateException(transactionException, rollbackException));
            }

            throw;
        }
    }

    public async Task RecoverPendingAsync(
        DataRootLayout layout,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(layout.TransactionsDirectory))
        {
            return;
        }

        foreach (var transactionRoot in Directory.EnumerateDirectories(layout.TransactionsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifestPath = Path.Combine(transactionRoot, ManifestFileName);
            TransactionDocument document;
            try
            {
                document = await _store.ReadAsync<TransactionDocument>(
                    manifestPath,
                    ValidateManifest,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or JsonException or DataPersistenceException)
            {
                throw new CriticalDataFileException(manifestPath, exception);
            }

            if (document.State == TransactionState.Committed)
            {
                Directory.Delete(transactionRoot, recursive: true);
                continue;
            }

            var rollbackRoot = Path.Combine(transactionRoot, "rollback");
            Rollback(
                layout,
                rollbackRoot,
                document.Entries,
                document.State == TransactionState.Applying);
            Directory.Delete(transactionRoot, recursive: true);
        }
    }

    private Task SaveManifestAsync(
        string manifestPath,
        string transactionId,
        TransactionState state,
        IReadOnlyList<TransactionEntryDocument> entries,
        CancellationToken cancellationToken)
    {
        var document = new TransactionDocument(
            JsonDefaults.CurrentSchemaVersion,
            transactionId,
            state,
            entries);
        return _store.SaveAsync(manifestPath, document, ValidateManifest, cancellationToken);
    }

    private static void ValidateManifest(TransactionDocument document)
    {
        SchemaVersionGuard.EnsureCurrent(document.SchemaVersion);
        if (!Guid.TryParse(document.Id, out var transactionId)
            || transactionId.ToString("D")[14] != '7'
            || document.Entries.Count == 0
            || document.Entries.Select(entry => entry.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != document.Entries.Count)
        {
            throw new DataPersistenceException("transaction.jsonの内容が正しくありません。");
        }
    }

    private static void ValidateChanges(DataRootLayout layout, JsonFileChange[] changes)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in changes)
        {
            _ = layout.ResolveRelativePath(change.RelativePath);
            if (change.DeleteTarget != (change.Content is null))
            {
                throw new ArgumentException("削除変更にはJSON内容を指定できません。", nameof(changes));
            }

            if (!paths.Add(NormalizeRelativePath(change.RelativePath)))
            {
                throw new ArgumentException("同じファイルを複数回更新できません。", nameof(changes));
            }
        }
    }

    private static void Rollback(
        DataRootLayout layout,
        string rollbackRoot,
        IReadOnlyList<TransactionEntryDocument> entries,
        bool changesMayHaveBeenApplied)
    {
        if (!changesMayHaveBeenApplied)
        {
            return;
        }

        foreach (var entry in entries)
        {
            var targetPath = layout.ResolveRelativePath(entry.RelativePath);
            if (entry.ExistedBeforeTransaction)
            {
                var rollbackPath = ResolveTransactionPath(rollbackRoot, entry.RelativePath);
                if (!File.Exists(rollbackPath))
                {
                    throw new IOException($"ロールバックファイルがありません: {rollbackPath}");
                }

                var targetDirectory = Path.GetDirectoryName(targetPath)
                    ?? throw new IOException("復元先を解決できません。");
                _ = Directory.CreateDirectory(targetDirectory);
                CopyAndFlush(rollbackPath, targetPath, overwrite: true);
            }
            else if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    private static string ResolveTransactionPath(string transactionAreaRoot, string relativePath)
    {
        var resolved = Path.GetFullPath(Path.Combine(transactionAreaRoot, relativePath));
        var relative = Path.GetRelativePath(transactionAreaRoot, resolved);
        if (relative == ".."
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            throw new DataPersistenceException("トランザクション領域外のパスが指定されています。");
        }

        return resolved;
    }

    private static void CopyAndFlush(string sourcePath, string destinationPath, bool overwrite)
    {
        File.Copy(sourcePath, destinationPath, overwrite);
        using var stream = new FileStream(
            destinationPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read);
        stream.Flush(flushToDisk: true);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }
}
