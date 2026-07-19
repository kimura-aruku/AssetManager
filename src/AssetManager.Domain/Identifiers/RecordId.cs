namespace AssetManager.Domain.Identifiers;

public readonly record struct RecordId
{
    public RecordId(Guid value)
    {
        IdentifierValidation.EnsureUuidVersion7(value, nameof(value));
        Value = value;
    }

    public Guid Value { get; }

    public static RecordId New()
    {
        return new RecordId(Guid.CreateVersion7());
    }

    public static RecordId Parse(string value)
    {
        return new RecordId(Guid.Parse(value));
    }

    public override string ToString()
    {
        return Value.ToString("D");
    }
}
