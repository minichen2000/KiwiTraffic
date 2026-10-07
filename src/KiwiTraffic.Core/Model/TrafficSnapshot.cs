namespace KiwiTraffic.Core.Model;

/// <summary>
/// A normalized, contract-independent reading of one VPS at one point in time.
/// This is the unit that gets cached; only a fully validated response may
/// produce one.
/// </summary>
public sealed record TrafficSnapshot
{
    /// <summary>Identifier of the configuration this reading belongs to.</summary>
    /// <remarks>
    /// A cache entry is only valid for the profile that produced it; switching
    /// VPS must never surface the previous machine's numbers.
    /// </remarks>
    public required string ProfileId { get; init; }

    /// <summary>The KiwiVM VEID this reading was fetched for.</summary>
    public required long Veid { get; init; }

    /// <summary>Traffic used in the current cycle, in bytes.</summary>
    public required decimal UsedBytes { get; init; }

    /// <summary>
    /// Cycle quota in bytes, or <c>null</c> when the contract defines it as
    /// missing or unusable. Never substitute 0 for "unknown".
    /// </summary>
    public required decimal? QuotaBytes { get; init; }

    /// <summary>
    /// When the provider says the counter resets, or <c>null</c> when that is
    /// unknown. Never assume the first of the month or a fixed 30-day cycle.
    /// </summary>
    public required DateTimeOffset? NextResetAtUtc { get; init; }

    /// <summary>When this reading was successfully fetched (UTC).</summary>
    public required DateTimeOffset FetchedAtUtc { get; init; }

    /// <summary>Computes the display metrics for this reading.</summary>
    public TrafficUsage ToUsage() =>
        KiwiTraffic.Core.Calculation.UsageCalculator.Calculate(UsedBytes, QuotaBytes);
}
