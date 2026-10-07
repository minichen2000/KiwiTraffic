using System.Text.Json;
using System.Text.Json.Serialization;
using KiwiTraffic.Infrastructure.KiwiVm.Json;

namespace KiwiTraffic.Infrastructure.KiwiVm;

/// <summary>
/// The subset of the <c>getServiceInfo</c> response this application needs.
/// </summary>
/// <remarks>
/// The real response carries about forty fields; unmodelled ones are ignored.
/// Field names and semantics are recorded in <c>docs/api-contract.md</c>.
/// </remarks>
public sealed class ServiceInfoResponse
{
    /// <summary><c>0</c> means success. Anything else is a business error.</summary>
    [JsonConverter(typeof(LenientInt64Converter))]
    public long? Error { get; set; }

    /// <summary>Human-readable error message, present when <see cref="Error"/> is non-zero.</summary>
    public string? Message { get; set; }

    /// <summary>Plan name.</summary>
    public string? Plan { get; set; }

    public string? Hostname { get; set; }

    /// <summary>Cycle quota in <em>bytes</em>. May be absent or unusable.</summary>
    [JsonConverter(typeof(LenientDecimalConverter))]
    public decimal? PlanMonthlyData { get; set; }

    /// <summary>Traffic used in the current cycle, in <em>bytes</em>.</summary>
    [JsonConverter(typeof(LenientDecimalConverter))]
    public decimal? DataCounter { get; set; }

    /// <summary>Bandwidth accounting coefficient. See the multiplier policy.</summary>
    [JsonConverter(typeof(LenientDecimalConverter))]
    public decimal? MonthlyDataMultiplier { get; set; }

    /// <summary>Reset instant as a Unix timestamp in <em>seconds</em>.</summary>
    [JsonConverter(typeof(LenientInt64Converter))]
    public long? DataNextReset { get; set; }
}

/// <summary>Serialization settings for KiwiVM API payloads.</summary>
public static class KiwiVmJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        // plan_monthly_data, data_next_reset, ...
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,

        // The response carries many more fields than we model; unlike the local
        // config files, unknown members here are expected and must be ignored.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };
}
