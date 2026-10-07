namespace KiwiTraffic.Infrastructure.KiwiVm;

/// <summary>
/// How <c>monthly_data_multiplier</c> is applied. See
/// <c>docs/api-contract.md</c> section 2 - the two readings of the official
/// documentation disagree, and only a live account can settle it.
/// </summary>
/// <remarks>
/// With <c>c</c> = data_counter, <c>pm</c> = plan_monthly_data and
/// <c>m</c> = the multiplier:
/// <code>
/// ScaleBoth:      used = c * m        quota = pm * m     percent = c / pm
/// ScaleQuotaOnly: used = c            quota = pm * m     percent = c / (pm * m)
/// </code>
/// Both agree on the quota the panel shows (<c>pm * m</c>), so the quota cannot
/// be used to tell them apart - only the displayed <em>used</em> figure can.
/// </remarks>
public enum MultiplierPolicy
{
    /// <summary>
    /// The official documentation: "needs to be multiplied by
    /// monthly_data_multiplier" for both the quota and the counter. The
    /// multiplier then cancels in the percentage.
    /// </summary>
    ScaleBoth,

    /// <summary>
    /// What the panel appears to do: the multiplier is a per-datacenter quota
    /// coefficient (CN2 GT is 0.33x, i.e. a third of the quota), and the
    /// counter is already reported in panel units.
    /// </summary>
    ScaleQuotaOnly,
}
