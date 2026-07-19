using System.Text.Json;

namespace AssetManager.Infrastructure.Persistence.Json;

public sealed class AtomicJsonFileStore
{
    private readonly JsonSerializerOptions _options;

    public const string RollbackSuffix = ".rollback";
    public const string TemporarySuffix = ".tmp";

    public AtomicJsonFileStore(JsonSerializerOptions? options = null)
    {
        _options = options ?? JsonDefaults.Options;
    }

    public async Task<T> ReadAsync<T>(
        string path,
        Action<T>? validate = null,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var value = await JsonSerializer.DeserializeAsync<T>(
            stream,
            _options,
            cancellationToken).ConfigureAwait(false);

        if (value is null)
        {
            throw new JsonException($"'{path}'をJSONオブジェクトとして読み込めませんでした。");
        }

        validate?.Invoke(value);
        return value;
    }

    public async Task SaveAsync<T>(
        string path,
        T value,
        Action<T>? validate = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(value);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("保存先ディレクトリを解決できません。", nameof(path));
        _ = Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}{TemporarySuffix}");
        var rollbackPath = GetRollbackPath(fullPath);
        var replacingExistingFile = File.Exists(fullPath);

        if (File.Exists(rollbackPath))
        {
            throw new IOException($"復旧待ちのロールバックファイルがあります: {rollbackPath}");
        }

        try
        {
            await WriteAndFlushAsync(temporaryPath, value, cancellationToken).ConfigureAwait(false);
            _ = await ReadAsync(temporaryPath, validate, cancellationToken).ConfigureAwait(false);

            if (replacingExistingFile)
            {
                File.Replace(temporaryPath, fullPath, rollbackPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }

            try
            {
                _ = await ReadAsync(fullPath, validate, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                RestoreAfterFailedVerification(fullPath, rollbackPath, replacingExistingFile);
                throw;
            }

            if (File.Exists(rollbackPath))
            {
                File.Delete(rollbackPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public async Task<bool> IsValidJsonAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await ReadAsync<JsonElement>(path, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    public static string GetRollbackPath(string targetPath)
    {
        return $"{targetPath}{RollbackSuffix}";
    }

    private async Task WriteAndFlushAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await JsonSerializer.SerializeAsync(
            stream,
            value,
            _options,
            cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static void RestoreAfterFailedVerification(
        string targetPath,
        string rollbackPath,
        bool replacingExistingFile)
    {
        if (replacingExistingFile && File.Exists(rollbackPath))
        {
            File.Replace(rollbackPath, targetPath, null, ignoreMetadataErrors: true);
            return;
        }

        if (!replacingExistingFile && File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }
    }
}
