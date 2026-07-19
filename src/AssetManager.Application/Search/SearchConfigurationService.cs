namespace AssetManager.Application.Search;

public sealed class SearchConfigurationService(IViewConfigurationStore store)
{
    private readonly IViewConfigurationStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<SavedSearch>> LoadSavedSearchesAsync(
        CancellationToken cancellationToken = default)
    {
        return _store.LoadSavedSearchesAsync(cancellationToken);
    }

    public async Task<SavedSearch> SaveSearchAsync(
        string name,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var searches = await _store.LoadSavedSearchesAsync(cancellationToken).ConfigureAwait(false);
        var saved = SavedSearch.Create(name, query);
        await _store.SaveSavedSearchesAsync(
            searches.Append(saved).ToArray(),
            cancellationToken).ConfigureAwait(false);
        return saved;
    }

    public async Task DeleteSearchAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var searches = await _store.LoadSavedSearchesAsync(cancellationToken).ConfigureAwait(false);
        if (searches.All(search => search.Id != id))
        {
            throw new KeyNotFoundException($"保存済み検索'{id}'が見つかりません。");
        }

        await _store.SaveSavedSearchesAsync(
            searches.Where(search => search.Id != id).ToArray(),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<ViewColumnSetting>> LoadViewColumnsAsync(
        CancellationToken cancellationToken = default)
    {
        return _store.LoadViewColumnsAsync(cancellationToken);
    }

    public Task SaveViewColumnsAsync(
        IReadOnlyList<ViewColumnSetting> columns,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var validated = columns.Select(column => column.Validate()).ToArray();
        if (validated.Select(column => column.Id).Distinct(StringComparer.Ordinal).Count()
            != validated.Length
            || validated.Select(column => column.Order).Distinct().Count() != validated.Length)
        {
            throw new ArgumentException("表示カラムのIDまたは順序が重複しています。", nameof(columns));
        }

        return _store.SaveViewColumnsAsync(validated, cancellationToken);
    }
}
