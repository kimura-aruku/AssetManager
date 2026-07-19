using System.Collections.ObjectModel;
using AssetManager.Domain.Identifiers;

namespace AssetManager.Application.Search;

public abstract record FieldSearchCondition;

public sealed record TextContainsCondition : FieldSearchCondition
{
    public TextContainsCondition(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        Query = query;
    }

    public string Query { get; }
}

public sealed record BooleanEqualsCondition(bool Value) : FieldSearchCondition;

public sealed record OptionAnyCondition : FieldSearchCondition
{
    private readonly ReadOnlyCollection<string> _optionIds;

    public OptionAnyCondition(IEnumerable<string> optionIds)
    {
        ArgumentNullException.ThrowIfNull(optionIds);
        var copied = optionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (copied.Length == 0)
        {
            throw new ArgumentException("1件以上の検索候補が必要です。", nameof(optionIds));
        }

        _optionIds = Array.AsReadOnly(copied);
    }

    public IReadOnlyList<string> OptionIds => _optionIds;
}

public sealed record SearchQuery
{
    private readonly ReadOnlyDictionary<FieldId, FieldSearchCondition> _conditions;

    public SearchQuery(IEnumerable<KeyValuePair<FieldId, FieldSearchCondition>>? conditions = null)
    {
        var copied = conditions?.ToDictionary() ?? [];
        if (copied.Values.Any(condition => condition is null))
        {
            throw new ArgumentException("検索条件にnullは指定できません。", nameof(conditions));
        }

        _conditions = new ReadOnlyDictionary<FieldId, FieldSearchCondition>(copied);
    }

    public IReadOnlyDictionary<FieldId, FieldSearchCondition> Conditions => _conditions;
}

public sealed record SavedSearch(Guid Id, string Name, SearchQuery Query)
{
    public static SavedSearch Create(string name, SearchQuery query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(query);
        return new SavedSearch(Guid.CreateVersion7(), name, query);
    }
}

public sealed record ViewColumnSetting(string Id, int Order, double Width, bool Visible)
{
    public ViewColumnSetting Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentOutOfRangeException.ThrowIfNegative(Order);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Width, 0);
        return this;
    }
}

public interface IViewConfigurationStore
{
    Task<IReadOnlyList<SavedSearch>> LoadSavedSearchesAsync(
        CancellationToken cancellationToken = default);

    Task SaveSavedSearchesAsync(
        IReadOnlyList<SavedSearch> searches,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ViewColumnSetting>> LoadViewColumnsAsync(
        CancellationToken cancellationToken = default);

    Task SaveViewColumnsAsync(
        IReadOnlyList<ViewColumnSetting> columns,
        CancellationToken cancellationToken = default);
}
