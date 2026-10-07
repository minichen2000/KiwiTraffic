using System.Globalization;
using KiwiTraffic.Core.Formatting;
using KiwiTraffic.Core.Model;

namespace KiwiTraffic.App.ViewModels;

/// <summary>
/// Read-only projection of one snapshot into display text.
/// </summary>
/// <remarks>
/// TEMPORARY (M1). This is a preview projection so the layout and the wording
/// can be developed before the API contract is verified; the real widget view
/// model arrives with the window and tray work in M3. It never talks to the
/// network and it never formats raw API fields - everything comes from
/// <see cref="TrafficUsage"/>.
/// </remarks>
public sealed class WidgetPreviewViewModel
{
    /// <summary>
    /// SI prefixes for now. Which family matches the KiwiVM panel is an open
    /// contract question (see <c>docs/api-contract.md</c>); until it is
    /// answered it stays a single constant here rather than a guess per call.
    /// </summary>
    private const BytePrefixStyle PrefixStyle = BytePrefixStyle.Si;

    public WidgetPreviewViewModel(string alias, TrafficSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var usage = snapshot.ToUsage();

        Alias = alias;
        ProgressValue = (double)usage.ProgressValue;

        PercentText = usage.UsedPercent is { } percent
            ? percent.ToString("F1", CultureInfo.InvariantCulture) + "%"
            : "额度未知";

        var used = ByteSizeFormatter.Format(usage.UsedBytes, PrefixStyle);
        UsedQuotaText = usage.QuotaBytes is { } quota
            ? $"已用 {used} / {ByteSizeFormatter.Format(quota, PrefixStyle)}"
            : $"已用 {used} / 额度未知";

        RemainingText = usage.RemainingBytes is { } remaining
            ? $"剩余 {ByteSizeFormatter.Format(remaining, PrefixStyle)}"
            : "剩余未知";

        ResetText = DescribeReset(snapshot.NextResetAtUtc, now);
        UpdatedText = $"更新于 {snapshot.FetchedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}";
    }

    public string Alias { get; }

    public string PercentText { get; }

    public double ProgressValue { get; }

    public string UsedQuotaText { get; }

    public string RemainingText { get; }

    public string ResetText { get; }

    public string UpdatedText { get; }

    /// <summary>
    /// The reset time always comes from the provider. An expired value is
    /// reported as "waiting for the provider to update" - never silently
    /// treated as a fresh cycle with a zeroed counter.
    /// </summary>
    private static string DescribeReset(DateTimeOffset? nextResetAtUtc, DateTimeOffset now)
    {
        if (nextResetAtUtc is null)
        {
            return "重置时间未知";
        }

        if (nextResetAtUtc <= now)
        {
            return "待服务商更新重置时间";
        }

        var local = nextResetAtUtc.Value.ToLocalTime();
        var days = (int)Math.Ceiling((nextResetAtUtc.Value - now).TotalDays);

        return $"重置 {local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}（还有 {days} 天）";
    }
}
