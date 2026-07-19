using AssetManager.Infrastructure.Persistence.Json;

namespace AssetManager.Infrastructure.Persistence.Recovery;

public sealed record AtomicRecoveryResult(int RestoredFiles, int RemovedArtifacts);

public sealed class AtomicFileRecoveryService(AtomicJsonFileStore store)
{
    public async Task<AtomicRecoveryResult> RecoverAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return new AtomicRecoveryResult(0, 0);
        }

        var restoredFiles = 0;
        var removedArtifacts = 0;

        foreach (var rollbackPath in Directory
                     .EnumerateFiles(rootDirectory, $"*{AtomicJsonFileStore.RollbackSuffix}", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetPath = rollbackPath[..^AtomicJsonFileStore.RollbackSuffix.Length];
            var targetIsValid = File.Exists(targetPath)
                && await store.IsValidJsonAsync(targetPath, cancellationToken).ConfigureAwait(false);

            if (targetIsValid)
            {
                File.Delete(rollbackPath);
                removedArtifacts++;
                continue;
            }

            if (!await store.IsValidJsonAsync(rollbackPath, cancellationToken).ConfigureAwait(false))
            {
                throw new DataPersistenceException(
                    $"最終ファイルとロールバックファイルの両方が不正です: {targetPath}");
            }

            File.Move(rollbackPath, targetPath, overwrite: true);
            restoredFiles++;
        }

        foreach (var temporaryPath in Directory
                     .EnumerateFiles(rootDirectory, $"*{AtomicJsonFileStore.TemporarySuffix}", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetPath = TryResolveTemporaryTarget(temporaryPath);

            if (targetPath is not null
                && File.Exists(targetPath)
                && !await store.IsValidJsonAsync(targetPath, cancellationToken).ConfigureAwait(false))
            {
                throw new DataPersistenceException($"最終ファイルが不正なため一時ファイルを削除できません: {targetPath}");
            }

            File.Delete(temporaryPath);
            removedArtifacts++;
        }

        return new AtomicRecoveryResult(restoredFiles, removedArtifacts);
    }

    private static string? TryResolveTemporaryTarget(string temporaryPath)
    {
        var fileName = Path.GetFileName(temporaryPath);
        const int generatedSuffixLength = 1 + 32 + 4;
        if (!fileName.StartsWith('.')
            || !fileName.EndsWith(AtomicJsonFileStore.TemporarySuffix, StringComparison.Ordinal)
            || fileName.Length <= 1 + generatedSuffixLength)
        {
            return null;
        }

        var originalNameLength = fileName.Length - 1 - generatedSuffixLength;
        var originalName = fileName.Substring(1, originalNameLength);
        return Path.Combine(Path.GetDirectoryName(temporaryPath)!, originalName);
    }
}
