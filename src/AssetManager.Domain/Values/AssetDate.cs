using System.Globalization;
using AssetManager.Domain.Common;

namespace AssetManager.Domain.Values;

public readonly record struct AssetDate(DateOnly Value)
{
    public const string StorageFormat = "yyyy-MM-dd";
    public const string DisplayFormat = "yyyy/MM/dd";

    public static AssetDate ParseStorage(string value)
    {
        return ParseExact(value, StorageFormat, "内部日付はYYYY-MM-DD形式で指定してください。");
    }

    public static AssetDate ParseDisplay(string value)
    {
        return ParseExact(value, DisplayFormat, "表示日付はYYYY/MM/DD形式で指定してください。");
    }

    public string ToStorageString()
    {
        return Value.ToString(StorageFormat, CultureInfo.InvariantCulture);
    }

    public string ToDisplayString()
    {
        return Value.ToString(DisplayFormat, CultureInfo.InvariantCulture);
    }

    public override string ToString()
    {
        return ToStorageString();
    }

    private static AssetDate ParseExact(string value, string format, string errorMessage)
    {
        if (!DateOnly.TryParseExact(
                value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var result))
        {
            throw new DomainValidationException(errorMessage, nameof(value));
        }

        return new AssetDate(result);
    }
}
