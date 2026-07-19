using System.Globalization;
using System.Text;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.Application.Search;

public static class RecordSearchEngine
{
    public static IReadOnlyList<AssetRecord> Filter(
        IEnumerable<AssetRecord> records,
        SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(query);
        return records.Where(record => Matches(record, query)).ToArray();
    }

    public static bool Matches(AssetRecord record, SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(query);
        return query.Conditions.All(pair => MatchesCondition(record, pair.Key, pair.Value));
    }

    private static bool MatchesCondition(
        AssetRecord record,
        FieldId fieldId,
        FieldSearchCondition condition)
    {
        _ = record.Values.TryGetValue(fieldId, out var value);
        return condition switch
        {
            BooleanEqualsCondition boolean => value is BooleanFieldValue item
                ? item.Value == boolean.Value
                : !boolean.Value,
            OptionAnyCondition options => value is not null && GetOptionIds(value)
                .Intersect(options.OptionIds, StringComparer.Ordinal)
                .Any(),
            TextContainsCondition text => value is not null
                && MatchesText(GetSearchableText(value), text.Query),
            _ => throw new ArgumentOutOfRangeException(nameof(condition)),
        };
    }

    private static bool MatchesText(string value, string query)
    {
        var normalizedValue = Normalize(value);
        var terms = Normalize(query).Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return terms.All(term => normalizedValue.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value)
    {
        return value.Normalize(NormalizationForm.FormKC);
    }

    private static IEnumerable<string> GetOptionIds(FieldValue value)
    {
        return value switch
        {
            SingleSelectionFieldValue item => [item.Value.Value],
            MultiSelectionFieldValue item => item.Values.Select(id => id.Value),
            AssetTypeSetFieldValue item => item.Values.Select(id => id.Value),
            TagSetFieldValue item => item.Values.Select(id => id.Value),
            RecordStatusFieldValue item => [item.Value.ToString().ToLowerInvariant()],
            _ => [],
        };
    }

    private static string GetSearchableText(FieldValue value)
    {
        return value switch
        {
            TextFieldValue item => item.Value,
            MultilineTextFieldValue item => item.Value,
            NumberFieldValue item => item.Value.ToString(CultureInfo.InvariantCulture),
            DateFieldValue item => $"{item.Value.ToStorageString()} {item.Value.ToDisplayString()}",
            UrlFieldValue item => item.Value,
            FilePathFieldValue item => item.Value,
            FolderPathFieldValue item => item.Value,
            TargetPathFieldValue item => item.Path,
            CreatorListFieldValue item => string.Join(' ', item.Values),
            SellerListFieldValue item => string.Join(' ', item.Values),
            RelatedDocumentListFieldValue item => string.Join(
                ' ',
                item.Values.Select(document => $"{document.Title} {document.Path}")),
            RelatedUrlListFieldValue item => string.Join(
                ' ',
                item.Values.Select(url => $"{url.Title} {url.Url}")),
            _ => string.Empty,
        };
    }
}
