using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KiwiTraffic.Infrastructure.KiwiVm.Json;

/// <summary>
/// Reads a value that may be a JSON number, a numeric string, or null.
/// </summary>
/// <remarks>
/// KiwiVM is inconsistent about this: the same field can arrive as
/// <c>4294967296</c> or <c>"4294967296"</c> depending on the endpoint or the
/// account (confirmed for <c>plan_disk</c>, <c>location_ipv6_ready</c> and
/// friends - see docs/api-contract.md section 9). A value that looks like a
/// number but does not parse is a <see cref="JsonException"/>, never a silent
/// zero: an unreadable reading must not replace the last valid snapshot.
/// </remarks>
internal static class LenientNumber
{
    public static bool TryParseDecimal(string? raw, out decimal value)
    {
        value = 0m;
        return !string.IsNullOrWhiteSpace(raw)
            && decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryParseInt64(string? raw, out long value)
    {
        value = 0L;
        return !string.IsNullOrWhiteSpace(raw)
            && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}

/// <summary>JSON number or numeric string -&gt; <see cref="decimal"/>.</summary>
public sealed class LenientDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number when reader.TryGetDecimal(out var number) => number,
            JsonTokenType.Number => throw new JsonException("数值超出 decimal 的可表示范围。"),
            JsonTokenType.String when LenientNumber.TryParseDecimal(reader.GetString(), out var parsed) => parsed,
            JsonTokenType.String => throw new JsonException("字符串不是有效的数字。"),
            _ => throw new JsonException($"期望数字、数字字符串或 null，实际是 {reader.TokenType}。"),
        };

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
        => throw new NotSupportedException("KiwiVM 请求从不回写该字段。");
}

/// <summary>JSON number or numeric string -&gt; <see cref="long"/>.</summary>
public sealed class LenientInt64Converter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number when reader.TryGetInt64(out var number) => number,
            JsonTokenType.Number => throw new JsonException("数值超出 64 位整数的可表示范围。"),
            JsonTokenType.String when LenientNumber.TryParseInt64(reader.GetString(), out var parsed) => parsed,
            JsonTokenType.String => throw new JsonException("字符串不是有效的整数。"),
            _ => throw new JsonException($"期望数字、数字字符串或 null，实际是 {reader.TokenType}。"),
        };

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
        => throw new NotSupportedException("KiwiVM 请求从不回写该字段。");
}
