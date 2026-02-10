using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderXChange.Application.Integrations.Talabat;

/// <summary>
/// Allows reading numeric/boolean JSON tokens into string properties.
/// Talabat payloads may send values as either string or number.
/// </summary>
public sealed class TalabatFlexibleStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => ReadNumberAsString(ref reader),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unsupported token '{reader.TokenType}' for flexible string conversion.")
        };
    }

    private static string ReadNumberAsString(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var int64Value))
        {
            return int64Value.ToString(CultureInfo.InvariantCulture);
        }

        if (reader.TryGetDecimal(out var decimalValue))
        {
            return decimalValue.ToString(CultureInfo.InvariantCulture);
        }

        if (reader.TryGetDouble(out var doubleValue))
        {
            return doubleValue.ToString(CultureInfo.InvariantCulture);
        }

        throw new JsonException("Unsupported numeric format for flexible string conversion.");
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }
}
