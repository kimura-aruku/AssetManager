using AssetManager.Application.Data;
using AssetManager.Domain.Records;

namespace AssetManager.Application.Paths;

public sealed class RecordPathCheckCoordinator
{
    private readonly IAssetManagerDataStore _store;
    private readonly PathCheckService _pathChecks;

    public RecordPathCheckCoordinator(
        IAssetManagerDataStore store,
        PathCheckService pathChecks)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _pathChecks = pathChecks ?? throw new ArgumentNullException(nameof(pathChecks));
    }

    public IReadOnlyDictionary<string, PathCheckResult> Cache => _pathChecks.Cache;

    public async Task<PathCheckBatchResult> CheckAllRecordsAsync(
        bool refreshCachedResults,
        IProgress<PathCheckProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var paths = PathReferenceExtractor.Extract(snapshot.Records);
        return await _pathChecks.CheckAllAsync(
            paths,
            refreshCachedResults,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<PathCheckResult> CheckChangedPathAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        return _pathChecks.CheckChangedPathAsync(path, cancellationToken);
    }

    public Task<PathCheckBatchResult> CheckRecordAsync(
        AssetRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return _pathChecks.CheckAllAsync(
            PathReferenceExtractor.Extract([record]),
            refreshCachedResults: true,
            cancellationToken: cancellationToken);
    }
}
