using System.Globalization;

namespace KiwiTraffic.Core.Formatting;

/// <summary>
/// Describes how long ago something happened, in words.
/// </summary>
/// <remarks>
/// Pure so it can be tested, and deliberately separate from the widget: the
/// plan requires the relative label to keep moving, which means it has to be
/// recomputed on a timer rather than once when the reading arrives.
/// </remarks>
public static class RelativeTime
{
    /// <summary>Below this, "just now" is accurate enough.</summary>
    public static readonly TimeSpan JustNow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// A human phrase for <paramref name="age"/>. Negative ages (a clock that
    /// jumped backwards, or a server timestamp slightly ahead) read as "just now"
    /// rather than as something absurd.
    /// </summary>
    public static string Describe(TimeSpan age)
    {
        if (age < JustNow)
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

    /// <summary>
    /// How long until a future moment, for a countdown.
    /// </summary>
    /// <remarks>
    /// Rounds <em>up</em>: with twelve hours left, "1 天" is honest and "0 天"
    /// is not. Returns just the amount - the caller supplies the wording.
    /// </remarks>
    public static string DescribeUntil(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return "0 分钟";
        }

        if (remaining < TimeSpan.FromHours(1))
        {
            return $"{(int)Math.Ceiling(remaining.TotalMinutes)} 分钟";
        }

        if (remaining < TimeSpan.FromDays(1))
        {
            return $"{(int)Math.Ceiling(remaining.TotalHours)} 小时";
        }

        return $"{(int)Math.Ceiling(remaining.TotalDays)} 天";
    }

    /// <summary>
    /// The moment itself, short enough for the widget. Today's readings show
    /// only the clock time; older ones add the date, because "12:34" on its own
    /// would be ambiguous.
    /// </summary>
    public static string Stamp(DateTimeOffset moment, DateTimeOffset now)
    {
        var local = moment.ToLocalTime();

        return local.Date == now.ToLocalTime().Date
            ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
            : local.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
