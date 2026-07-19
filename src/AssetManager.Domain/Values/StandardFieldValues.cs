using System.Collections.ObjectModel;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;

namespace AssetManager.Domain.Values;

public enum RecordStatus
{
    Unchecked,
    Available,
    Unavailable,
    Archived,
}

public readonly record struct FavoriteStatus(bool IsFavorite)
{
    public static FavoriteStatus Favorite { get; } = new(true);

    public static FavoriteStatus NotFavorite { get; } = new(false);
}

public sealed record RecordStatusFieldValue(RecordStatus Value) : FieldValue
{
    public override FieldType Type => FieldType.RecordStatus;
}

public sealed record CreatorListFieldValue : FieldValue
{
    private readonly ReadOnlyCollection<string> _values;

    public CreatorListFieldValue(IEnumerable<string> values)
    {
        _values = Array.AsReadOnly(ValueValidation.CopyUniqueText(values, nameof(values)));
    }

    public IReadOnlyList<string> Values => _values;

    public override FieldType Type => FieldType.StringList;
}

public sealed record SellerListFieldValue : FieldValue
{
    private readonly ReadOnlyCollection<string> _values;

    public SellerListFieldValue(IEnumerable<string> values)
    {
        _values = Array.AsReadOnly(ValueValidation.CopyUniqueText(values, nameof(values)));
    }

    public IReadOnlyList<string> Values => _values;

    public override FieldType Type => FieldType.StringList;
}

public sealed record RelatedDocument
{
    public RelatedDocument(string title, string path)
    {
        Title = ValueValidation.RequireText(title, nameof(title));
        Path = ValueValidation.RequireText(path, nameof(path));
    }

    public string Title { get; }

    public string Path { get; }
}

public sealed record RelatedDocumentListFieldValue : FieldValue
{
    private readonly ReadOnlyCollection<RelatedDocument> _values;

    public RelatedDocumentListFieldValue(IEnumerable<RelatedDocument> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copiedValues = values.ToArray();

        if (copiedValues.Any(value => value is null))
        {
            throw new DomainValidationException("関連文書にnullは指定できません。", nameof(values));
        }

        _values = Array.AsReadOnly(copiedValues);
    }

    public IReadOnlyList<RelatedDocument> Values => _values;

    public override FieldType Type => FieldType.TitledPathList;
}

public sealed record RelatedUrl
{
    public RelatedUrl(string title, string url)
    {
        Title = ValueValidation.RequireText(title, nameof(title));
        Url = ValueValidation.RequireWebUrl(url, nameof(url));
    }

    public string Title { get; }

    public string Url { get; }
}

public sealed record RelatedUrlListFieldValue : FieldValue
{
    private readonly ReadOnlyCollection<RelatedUrl> _values;

    public RelatedUrlListFieldValue(IEnumerable<RelatedUrl> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copiedValues = values.ToArray();

        if (copiedValues.Any(value => value is null))
        {
            throw new DomainValidationException("関連URLにnullは指定できません。", nameof(values));
        }

        _values = Array.AsReadOnly(copiedValues);
    }

    public IReadOnlyList<RelatedUrl> Values => _values;

    public override FieldType Type => FieldType.TitledUrlList;
}
