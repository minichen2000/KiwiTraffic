using System.Globalization;
using KiwiTraffic.Core.Formatting;
using KiwiTraffic.Core.Model;
using KiwiTraffic.Core.Settings;

namespace KiwiTraffic.App.ViewModels;

/// <summary>
/// Projects one reading into display text.
/// </summary>
/// <remarks>
/// Everything here comes from <see cref="TrafficUsage"/>; no raw API field is
/// ever read or converted in the UI.
/// </remarks>
public sealed class WidgetViewModel : ObservableObject
{
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private UsageLevel _level = UsageLevel.Normal;

    private WidgetViewModel(string alias, IndicatorStyle style)
    {
        Alias = alias;
        Style = style;
    }

    /// <summary>Nothing readable yet - shows placeholders, never a fake 0 %.</summary>
    public static WidgetViewModel Loading(string alias, IndicatorStyle style) => new(alias, style)
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
        DateTimeOffset now,
        IndicatorStyle style)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var viewModel = new WidgetViewModel(alias, style);
        viewModel.Apply(snapshot.ToUsage(), snapshot, now);

        return viewModel;
    }

    public string Alias { get; }

    public IndicatorStyle Style { get; }

    public bool IsRingStyle => Style == IndicatorStyle.Ring;

    public bool IsBarStyle => Style == IndicatorStyle.Bar;

    /// <summary>False until a reading has been applied, so the gauge can stay hidden.</summary>
    public bool HasData => HasEverApplied;

    public string PercentText { get; private set; } = "—";

    public double ProgressValue { get; private set; }

    public UsageLevel Level
    {
        get => _level;
        private set => SetProperty(ref _level, value);
    }

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

    private bool HasEverApplied { get; set; }

    /// <summary>Re-renders the reading against a new "now" so relative times stay true.</summary>
    public void Apply(TrafficUsage usage, TrafficSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(snapshot);

        PercentText = usage.UsedPercent is { } percent
            ? percent.ToString("F1", CultureInfo.InvariantCulture) + "%"
            : "额度未知";

        ProgressValue = (double)usage.ProgressValue;

        // Colour is decoration; the same information is in the text either way.
        Level = UsageLevelClassifier.Classify(usage.UsedPercent);

        var used = ByteSizeFormatter.Format(usage.UsedBytes, DisplayFormat.BytePrefix);
        UsedQuotaText = usage.QuotaBytes is { } quota
            ? $"已用 {used} / {ByteSizeFormatter.Format(quota, DisplayFormat.BytePrefix)}"
            : $"已用 {used} / 额度未知";

        RemainingText = usage.RemainingBytes is { } remaining
            ? $"剩余 {ByteSizeFormatter.Format(remaining, DisplayFormat.BytePrefix)}"
            : "剩余未知";

        ResetText = DescribeReset(snapshot.NextResetAtUtc, now);
        UpdatedText = $"更新于 {DescribeAge(snapshot.FetchedAtUtc, now)}";

        HasEverApplied = true;

        OnPropertyChanged(nameof(PercentText));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(UsedQuotaText));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(ResetText));
        OnPropertyChanged(nameof(UpdatedText));
        OnPropertyChanged(nameof(HasData));
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

        return $"{local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} 重置 · 还有 {days} 天";
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

        return age switch
        {
            { TotalMinutes: < 1 } => "刚刚",
            { TotalHours: < 1 } => $"{(int)age.TotalMinutes} 分钟前",
            { TotalDays: < 1 } => $"{(int)age.TotalHours} 小时前",
            _ => $"{(int)age.TotalDays} 天前",
        };
    }
}
