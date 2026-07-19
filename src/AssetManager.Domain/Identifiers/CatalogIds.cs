namespace AssetManager.Domain.Identifiers;

public readonly record struct AssetTypeId
{
    public AssetTypeId(string value)
    {
        Value = IdentifierValidation.EnsurePrefixedValue(value, "type.", nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct TagId
{
    public TagId(string value)
    {
        Value = IdentifierValidation.EnsurePrefixedValue(value, "tag.", nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct TagCategoryId
{
    public TagCategoryId(string value)
    {
        Value = IdentifierValidation.EnsurePrefixedValue(value, "tag-category.", nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct SelectionOptionId
{
    public SelectionOptionId(string value)
    {
        Value = IdentifierValidation.EnsureValue(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}
