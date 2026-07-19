using AssetManager.Domain.Common;

namespace AssetManager.Domain.Identifiers;

public readonly record struct FieldId
{
    public const string BuiltInPrefix = "builtin.";

    public FieldId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("カラムIDを指定してください。", nameof(value));
        }

        if (value.StartsWith(BuiltInPrefix, StringComparison.Ordinal))
        {
            IdentifierValidation.EnsurePrefixedValue(value, BuiltInPrefix, nameof(value));
        }
        else if (value.StartsWith(CustomFieldId.Prefix, StringComparison.Ordinal))
        {
            _ = CustomFieldId.Parse(value);
        }
        else
        {
            throw new DomainValidationException("組み込みまたはカスタムのカラムIDを指定してください。", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public bool IsBuiltIn => Value.StartsWith(BuiltInPrefix, StringComparison.Ordinal);

    public bool IsCustom => Value.StartsWith(CustomFieldId.Prefix, StringComparison.Ordinal);

    public static FieldId From(CustomFieldId value)
    {
        return new FieldId(value.ToString());
    }

    public override string ToString()
    {
        return Value;
    }
}
