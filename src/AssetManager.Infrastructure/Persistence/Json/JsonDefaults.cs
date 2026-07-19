using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetManager.Infrastructure.Persistence.Json;

public static class JsonDefaults
{
    public const int CurrentSchemaVersion = 1;

    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static byte[] SerializeToUtf8Bytes<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, Options);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

internal sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (text is null
            || !text.EndsWith('Z')
            || !DateTimeOffset.TryParse(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var value)
            || value.Offset != TimeSpan.Zero)
        {
            throw new JsonException("UTC日時は末尾ZのISO 8601形式で指定してください。");
        }

        return value;
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTimeOffset value,
        JsonSerializerOptions options)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new JsonException("UTC以外の日時は保存できません。");
        }

        writer.WriteStringValue(value.UtcDateTime);
    }
}
