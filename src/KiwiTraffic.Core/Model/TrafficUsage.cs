namespace KiwiTraffic.Core.Model;

/// <summary>
/// Display metrics derived from raw byte counts.
/// </summary>
/// <remarks>
/// A <c>null</c> optional value always means "unknown". It never means zero and
/// it never means "unlimited" - see the project rule "unknown is not zero".
/// </remarks>
public sealed record TrafficUsage
{
    /// <summary>Traffic used in the current billing cycle, in bytes. Never negative.</summary>
    public required decimal UsedBytes { get; init; }

    /// <summary>
    /// Cycle quota in bytes, or <c>null</c> when the source value is not usable
    /// (missing, zero or negative). A <c>null</c> quota suppresses both
    /// <see cref="UsedPercent"/> and <see cref="RemainingBytes"/>.
    /// </summary>
    public required decimal? QuotaBytes { get; init; }

    /// <summary>
    /// Unrounded used percentage. May exceed 100. <c>null</c> when the quota is
    /// unknown. Rounding for display, and threshold comparisons, are the
    /// caller's responsibility - never compare against a rounded copy.
    /// </summary>
    public required decimal? UsedPercent { get; init; }

    /// <summary>
    /// Remaining bytes, clamped at zero, or <c>null</c> when the quota is unknown.
    /// </summary>
    public required decimal? RemainingBytes { get; init; }

    /// <summary>
    /// Value for the progress bar: <see cref="UsedPercent"/> clamped to 0..100,
    /// or 0 when the percentage is unknown. The bar caps, the number does not.
    /// </summary>
    public required decimal ProgressValue { get; init; }

    /// <summary>True when the quota is usable and the percentage is meaningful.</summary>
    public bool IsQuotaKnown => QuotaBytes is not null;

    /// <summary>True when usage is above the quota. Only meaningful with a known quota.</summary>
    public bool IsOverQuota => UsedPercent is > KiwiTraffic.Core.Calculation.UsageCalculator.ProgressMaximum;
}
