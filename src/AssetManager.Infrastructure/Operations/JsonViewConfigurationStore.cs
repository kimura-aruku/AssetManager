using AssetManager.Application.Search;
using AssetManager.Domain.Identifiers;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Models;
using AssetManager.Infrastructure.Persistence.Repositories;

namespace AssetManager.Infrastructure.Operations;

public sealed class JsonViewConfigurationStore : IViewConfigurationStore
{
    private readonly DataRootLayout _layout;
    private readonly ViewSettingsRepository _repository;

    public JsonViewConfigurationStore(
        DataRootLayout layout,
        AtomicJsonFileStore? store = null)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _repository = new ViewSettingsRepository(store ?? new AtomicJsonFileStore());
    }

    public async Task<IReadOnlyList<SavedSearch>> LoadSavedSearchesAsync(
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.LoadAsync(_layout, cancellationToken).ConfigureAwait(false);
        return (document.SavedSearches ?? []).Select(ToDomain).ToArray();
    }

    public async Task SaveSavedSearchesAsync(
        IReadOnlyList<SavedSearch> searches,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searches);
        var document = await _repository.LoadAsync(_layout, cancellationToken).ConfigureAwait(false);
        await _repository.SaveAsync(
            _layout,
            document with { SavedSearches = searches.Select(ToDocument).ToArray() },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ViewColumnSetting>> LoadViewColumnsAsync(
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.LoadAsync(_layout, cancellationToken).ConfigureAwait(false);
        return document.MainTableColumns
            .Select(column => new ViewColumnSetting(
                column.Id,
                column.Order,
                column.Width,
                column.Visible))
            .OrderBy(column => column.Order)
            .ToArray();
    }

    public async Task SaveViewColumnsAsync(
        IReadOnlyList<ViewColumnSetting> columns,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var document = await _repository.LoadAsync(_layout, cancellationToken).ConfigureAwait(false);
        await _repository.SaveAsync(
            _layout,
            document with
            {
                MainTableColumns = columns
                    .Select(column => new ViewColumnDocument(
                        column.Id,
                        column.Order,
                        column.Width,
                        column.Visible))
                    .ToArray(),
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static SavedSearch ToDomain(SavedSearchDocument document)
    {
        return new SavedSearch(
            Guid.Parse(document.Id),
            document.Name,
            new SearchQuery(document.Conditions.Select(condition =>
                new KeyValuePair<FieldId, FieldSearchCondition>(
                    new FieldId(condition.FieldId),
                    ToDomain(condition)))));
    }

    private static FieldSearchCondition ToDomain(SavedSearchConditionDocument document)
    {
        return document.Kind switch
        {
            "text" => new TextContainsCondition(document.Text!),
            "boolean" => new BooleanEqualsCondition(document.Boolean!.Value),
            "optionAny" => new OptionAnyCondition(document.OptionIds!),
            _ => throw new InvalidDataException($"未対応の検索条件です: {document.Kind}"),
        };
    }

    private static SavedSearchDocument ToDocument(SavedSearch search)
    {
        return new SavedSearchDocument(
            search.Id.ToString("D"),
            search.Name,
            search.Query.Conditions.Select(pair => ToDocument(pair.Key, pair.Value)).ToArray());
    }

    private static SavedSearchConditionDocument ToDocument(
        FieldId fieldId,
        FieldSearchCondition condition)
    {
        return condition switch
        {
            TextContainsCondition text => new SavedSearchConditionDocument(
                fieldId.Value,
                "text",
                Text: text.Query),
            BooleanEqualsCondition boolean => new SavedSearchConditionDocument(
                fieldId.Value,
                "boolean",
                Boolean: boolean.Value),
            OptionAnyCondition options => new SavedSearchConditionDocument(
                fieldId.Value,
                "optionAny",
                OptionIds: options.OptionIds),
            _ => throw new ArgumentOutOfRangeException(nameof(condition)),
        };
    }
}
