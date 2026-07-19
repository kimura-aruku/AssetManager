using System.Text.RegularExpressions;
using AssetManager.Domain.Common;
using AssetManager.Domain.Identifiers;

namespace AssetManager.Domain.Catalog;

public readonly partial record struct TagColor
{
    public TagColor(string value)
    {
        if (!ColorPattern().IsMatch(value))
        {
            throw new DomainValidationException("タグ色は#RRGGBBまたは#AARRGGBB形式で指定してください。", nameof(value));
        }

        Value = value.ToUpperInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")]
    private static partial Regex ColorPattern();
}

public sealed record TagCategoryDefinition
{
    public TagCategoryDefinition(TagCategoryId id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("タグ分類名を指定してください。", nameof(name));
        }

        Id = id;
        Name = name;
    }

    public TagCategoryId Id { get; }

    public string Name { get; }
}

public sealed record TagDefinition
{
    public TagDefinition(
        TagId id,
        string name,
        TagColor color,
        TagCategoryId? categoryId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("タグ名を指定してください。", nameof(name));
        }

        Id = id;
        Name = name;
        Color = color;
        CategoryId = categoryId;
    }

    public TagId Id { get; }

    public string Name { get; }

    public TagColor Color { get; }

    public TagCategoryId? CategoryId { get; }
}
