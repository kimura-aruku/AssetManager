using AssetManager.Domain.Common;

namespace AssetManager.Domain.Identifiers;

public readonly record struct CustomFieldId
{
    public const string Prefix = "custom.";

    public CustomFieldId(Guid value)
    {
        IdentifierValidation.EnsureUuidVersion7(value, nameof(value));
        Value = value;
    }

    public Guid Value { get; }

    public static CustomFieldId New()
    {
        return new CustomFieldId(Guid.CreateVersion7());
    }

    public static CustomFieldId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new DomainValidationException("カスタムカラムIDの形式が正しくありません。", nameof(value));
        }

        return new CustomFieldId(Guid.Parse(value[Prefix.Length..]));
    }

    public override string ToString()
    {
        return $"{Prefix}{Value:D}";
    }
}
