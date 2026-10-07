namespace KiwiTraffic.Core.Model;

/// <summary>
/// Coarse banding of a usage percentage, used for the widget's accent colour.
/// </summary>
/// <remarks>
/// Colour is decoration only: the number and the text always carry the same
/// information, so this banding must never be the sole way a state is shown.
/// </remarks>
public enum UsageLevel
{
    /// <summary>Below the warning threshold, or unknown.</summary>
    Normal,

    /// <summary>At or above the warning threshold, below the critical one.</summary>
    Warning,

    /// <summary>At or above the critical threshold.</summary>
    Critical,
}

/// <summary>Thresholds behind <see cref="UsageLevel"/>. See plan section 4.2.</summary>
public static class UsageLevelClassifier
{
    public const decimal WarningPercent = 80m;
    public const decimal CriticalPercent = 90m;

    /// <summary>
    /// Classifies an unrounded percentage. An unknown percentage (no usable
    /// quota) is <see cref="UsageLevel.Normal"/>: it is not an alarm state, and
    /// the UI already says "quota unknown" in words.
    /// </summary>
    public static UsageLevel Classify(decimal? usedPercent) => usedPercent switch
    {
        null => UsageLevel.Normal,
        >= CriticalPercent => UsageLevel.Critical,
        >= WarningPercent => UsageLevel.Warning,
        _ => UsageLevel.Normal,
    };
}
