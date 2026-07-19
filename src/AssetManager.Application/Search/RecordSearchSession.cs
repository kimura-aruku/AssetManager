using AssetManager.Domain.Records;

namespace AssetManager.Application.Search;

public sealed class RecordSearchSession
{
    public const int PageSize = 100;

    private readonly IReadOnlyList<AssetRecord> _matches;
    private int _visibleCount;

    public RecordSearchSession(IEnumerable<AssetRecord> allRecords, SearchQuery query)
    {
        _matches = RecordSearchEngine.Filter(allRecords, query);
        _visibleCount = Math.Min(PageSize, _matches.Count);
    }

    public IReadOnlyList<AssetRecord> VisibleRecords => _matches.Take(_visibleCount).ToArray();

    public int TotalCount => _matches.Count;

    public bool HasMore => _visibleCount < _matches.Count;

    public IReadOnlyList<AssetRecord> LoadMore()
    {
        var previousCount = _visibleCount;
        _visibleCount = Math.Min(_visibleCount + PageSize, _matches.Count);
        return _matches.Skip(previousCount).Take(_visibleCount - previousCount).ToArray();
    }
}
