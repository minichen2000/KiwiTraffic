using System.Globalization;
using KiwiTraffic.Core.Formatting;
using KiwiTraffic.Core.Model;

namespace KiwiTraffic.App.ViewModels;

/// <summary>
/// Projects one reading into display text.
/// </summary>
/// <remarks>
/// Everything here comes from <see cref="TrafficUsage"/>; no raw API field is
/// ever read or converted in the UI. The real floating widget (position,
/// always-on-top, tray) arrives in M3 - this is the content, not the chrome.
/// </remarks>
public sealed class WidgetViewModel : ObservableObject
{
    /// <summary>
    /// SI prefixes for now. Which family matches the KiwiVM panel is an open
    /// contract question (see <c>docs/api-contract.md</c>); until it is
    /// answered it stays a single constant here rather than a guess per call.
    /// </summary>
    private const BytePrefixStyle PrefixStyle = BytePrefixStyle.Si;

    private string _statusMessage = string.Empty;
    private bool _isBusy;

    private WidgetViewModel(string alias)
    {
        Alias = alias;
    }

    /// <summary>Nothing readable yet - shows placeholders, never a fake 0 %.</summary>
    public static WidgetViewModel Loading(string alias) => new(alias)
    {
        PercentText = "—",
        UsedQuotaText = "正在读取…",
        RemainingText = string.Empty,
        ResetText = string.Empty,
        UpdatedText = string.Empty,
    };

    public static WidgetViewModel FromSnapshot(
        string alias,
        TrafficSnapshot snapshot,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var usage = snapshot.ToUsage();
        var viewModel = new WidgetViewModel(alias);

        viewModel.Apply(usage, snapshot, now);

        return viewModel;
    }

    public string Alias { get; }

    public string PercentText { get; private set; } = "—";

    public double ProgressValue { get; private set; }

    public string UsedQuotaText { get; private set; } = string.Empty;

    public string RemainingText { get; private set; } = string.Empty;

    public string ResetText { get; private set; } = string.Empty;

    public string UpdatedText { get; private set; } = string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    /// <summary>Re-renders the reading against a new "now" so relative times stay true.</summary>
    public void Apply(TrafficUsage usage, TrafficSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(snapshot);

        PercentText = usage.UsedPercent is { } percent
            ? percent.ToString("F1", CultureInfo.InvariantCulture) + "%"
            : "额度未知";

        ProgressValue = (double)usage.ProgressValue;

        var used = ByteSizeFormatter.Format(usage.UsedBytes, PrefixStyle);
        UsedQuotaText = usage.QuotaBytes is { } quota
            ? $"已用 {used} / {ByteSizeFormatter.Format(quota, PrefixStyle)}"
            : $"已用 {used} / 额度未知";

        RemainingText = usage.RemainingBytes is { } remaining
            ? $"剩余 {ByteSizeFormatter.Format(remaining, PrefixStyle)}"
            : "剩余未知";

        ResetText = DescribeReset(snapshot.NextResetAtUtc, now);
        UpdatedText = $"更新于 {DescribeAge(snapshot.FetchedAtUtc, now)}";

        OnPropertyChanged(nameof(PercentText));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(UsedQuotaText));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(ResetText));
        OnPropertyChanged(nameof(UpdatedText));
    }

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

    /// <summary>
    /// Relative to now, so the text keeps moving instead of getting stuck on
    /// "just now" - which would misrepresent a reading that stopped updating.
    /// </summary>
    private static string DescribeAge(DateTimeOffset fetchedAtUtc, DateTimeOffset now)
    {
        var age = now - fetchedAtUtc;

        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age < TimeSpan.FromMinutes(1))
        {
            return "刚刚";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes} 分钟前";
        }

        if (age < TimeSpan.FromDays(1))
        {
            return $"{(int)age.TotalHours} 小时前";
        }

        return $"{(int)age.TotalDays} 天前";
    }
}
