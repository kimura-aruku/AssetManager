using AssetManager.Domain.Common;

namespace AssetManager.Domain.Identifiers;

internal static class IdentifierValidation
{
    public static void EnsureUuidVersion7(Guid value, string parameterName)
    {
        if (value == Guid.Empty || value.ToString("D")[14] != '7')
        {
            throw new DomainValidationException("UUID Version 7を指定してください。", parameterName);
        }
    }

    public static string EnsurePrefixedValue(
        string value,
        string prefix,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith(prefix, StringComparison.Ordinal)
            || value.Length == prefix.Length)
        {
            throw new DomainValidationException($"'{prefix}'で始まるIDを指定してください。", parameterName);
        }

        return value;
    }

    public static string EnsureValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("IDを指定してください。", parameterName);
        }

        return value;
    }
}
