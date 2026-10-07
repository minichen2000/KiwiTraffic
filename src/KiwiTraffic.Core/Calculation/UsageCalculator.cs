using System.Diagnostics.CodeAnalysis;
using KiwiTraffic.Core.Model;

namespace KiwiTraffic.Core.Calculation;

/// <summary>
/// The only place where a used/quota byte pair is turned into percentages.
/// Pure, side-effect free and independent of the API contract: the caller is
/// responsible for having already converted the wire format into bytes.
/// </summary>
public static class UsageCalculator
{
    /// <summary>Percentage at which the progress bar is full. The number may go past it.</summary>
    public const decimal ProgressMaximum = 100m;

    /// <summary>
    /// Computes display metrics, throwing when the inputs cannot describe a
    /// measurement at all. Prefer <see cref="TryCalculate"/> in parsing code,
    /// where an invalid reading must not replace the last valid snapshot.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="usedBytes"/> is negative.
    /// </exception>
    public static TrafficUsage Calculate(decimal usedBytes, decimal? quotaBytes)
    {
        if (!TryCalculate(usedBytes, quotaBytes, out var usage))
        {
            throw new ArgumentOutOfRangeException(
                nameof(usedBytes),
                usedBytes,
                "Used traffic must not be negative. An invalid reading must not replace the last valid snapshot.");
        }

        return usage;
    }

    /// <summary>
    /// Computes display metrics from normalized byte counts.
    /// </summary>
    /// <returns>
    /// <c>false</c> when the inputs are not a usable measurement at all
    /// (negative usage, or a ratio too large to represent). Callers must keep
    /// the previous snapshot in that case.
    /// <para>
    /// A quota that is missing, zero or negative is <em>not</em> a failure: it
    /// yields a usage whose <see cref="TrafficUsage.QuotaBytes"/>,
    /// <see cref="TrafficUsage.UsedPercent"/> and
    /// <see cref="TrafficUsage.RemainingBytes"/> are all <c>null</c>, which the
    /// UI renders as "quota unknown". No division happens, and the data is
    /// never interpreted as unlimited traffic.
    /// </para>
    /// </returns>
    public static bool TryCalculate(decimal usedBytes, decimal? quotaBytes, [NotNullWhen(true)] out TrafficUsage? usage)
    {
        if (usedBytes < 0m)
        {
            usage = null;
            return false;
        }

        // Defence in depth: byte counts are integers in practice, but a
        // nonsensical quota (e.g. 1e-28) could push the ratio past the decimal
        // range. An overflowing ratio is an invalid reading, not a crash.
        if (quotaBytes is decimal quota && quota > 0m)
        {
            decimal percent;
            try
            {
                percent = usedBytes / quota * 100m;
            }
            catch (OverflowException)
            {
                usage = null;
                return false;
            }

            usage = new TrafficUsage
            {
                UsedBytes = usedBytes,
                QuotaBytes = quota,
                UsedPercent = percent,
                RemainingBytes = Math.Max(quota - usedBytes, 0m),
                ProgressValue = Math.Clamp(percent, 0m, ProgressMaximum),
            };
            return true;
        }

        usage = new TrafficUsage
        {
            UsedBytes = usedBytes,
            QuotaBytes = null,
            UsedPercent = null,
            RemainingBytes = null,
            ProgressValue = 0m,
        };
        return true;
    }
}
