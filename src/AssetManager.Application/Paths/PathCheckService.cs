using System.Collections.Concurrent;

namespace AssetManager.Application.Paths;

public sealed class PathCheckService
{
    public const int MaximumConcurrency = 8;

    private readonly IWindowsPathFileSystem _fileSystem;
    private readonly ConcurrentDictionary<string, PathCheckResult> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public PathCheckService(IWindowsPathFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public IReadOnlyDictionary<string, PathCheckResult> Cache => _cache;

    public async Task<PathCheckResult> CheckChangedPathAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var key = CreateKey(path);
        cancellationToken.ThrowIfCancellationRequested();
        _cache[key] = new PathCheckResult(key, PathCheckStatus.Checking);
        var result = await Task.Run(() => _fileSystem.Check(key), CancellationToken.None)
            .ConfigureAwait(false);
        _cache[key] = result;
        return result;
    }

    public async Task<PathCheckBatchResult> CheckAllAsync(
        IEnumerable<string> paths,
        bool refreshCachedResults,
        IProgress<PathCheckProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var uniquePaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(CreateKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var total = uniquePaths.Length;
        var checkedCount = 0;
        var nextIndex = -1;
        var resultMap = new ConcurrentDictionary<string, PathCheckResult>(StringComparer.OrdinalIgnoreCase);
        progress?.Report(new PathCheckProgress(0, total, total, false, null));

        async Task WorkerAsync()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var index = Interlocked.Increment(ref nextIndex);
                if (index >= total)
                {
                    return;
                }

                var path = uniquePaths[index];
                PathCheckResult result;
                if (!refreshCachedResults && _cache.TryGetValue(path, out var cached))
                {
                    result = cached;
                }
                else
                {
                    _cache[path] = new PathCheckResult(path, PathCheckStatus.Checking);
                    result = await Task.Run(() => _fileSystem.Check(path), CancellationToken.None)
                        .ConfigureAwait(false);
                    _cache[path] = result;
                }

                resultMap[path] = result;
                var completed = Interlocked.Increment(ref checkedCount);
                progress?.Report(new PathCheckProgress(
                    completed,
                    total,
                    total - completed,
                    false,
                    result));
            }
        }

        var workerCount = Math.Min(MaximumConcurrency, total);
        await Task.WhenAll(Enumerable.Range(0, workerCount).Select(_ => WorkerAsync()))
            .ConfigureAwait(false);
        var canceled = cancellationToken.IsCancellationRequested && checkedCount < total;
        if (canceled)
        {
            progress?.Report(new PathCheckProgress(
                checkedCount,
                total,
                total - checkedCount,
                true,
                null));
        }

        return new PathCheckBatchResult(resultMap, total, checkedCount, canceled);
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    private static string CreateKey(string path)
    {
        try
        {
            return WindowsPathNormalizer.CreateComparisonKey(path);
        }
        catch (WindowsPathValidationException)
        {
            return path;
        }
    }
}
