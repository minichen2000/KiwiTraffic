using KiwiTraffic.Core.Formatting;

namespace KiwiTraffic.Core.Tests;

public class RelativeTimeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(59)]
    public void UnderAMinute_ReadsAsJustNow(int seconds)
    {
        Assert.Equal("刚刚", RelativeTime.Describe(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(60, "1 分钟前")]
    [InlineData(61, "1 分钟前")]
    [InlineData(119, "1 分钟前")]
    [InlineData(120, "2 分钟前")]
    [InlineData(3599, "59 分钟前")]
    public void Minutes_AreRoundedDown(int seconds, string expected)
    {
        Assert.Equal(expected, RelativeTime.Describe(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(3600, "1 小时前")]
    [InlineData(7200, "2 小时前")]
    [InlineData(86399, "23 小时前")]
    public void Hours_AreRoundedDown(int seconds, string expected)
    {
        Assert.Equal(expected, RelativeTime.Describe(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(86400, "1 天前")]
    [InlineData(604800, "7 天前")]
    public void Days_AreRoundedDown(int seconds, string expected)
    {
        Assert.Equal(expected, RelativeTime.Describe(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void AClockThatJumpedBackwards_DoesNotReadAsTheFuture()
    {
        // A server timestamp slightly ahead of us, or an NTP correction.
        Assert.Equal("刚刚", RelativeTime.Describe(TimeSpan.FromMinutes(-5)));
    }

    [Fact]
    public void ThePhraseKeepsMoving_WhichIsTheWholePoint()
    {
        // The plan calls this out explicitly: the label must not be stuck on
        // "just now" while the data quietly goes stale.
        var labels = new[]
        {
            RelativeTime.Describe(TimeSpan.FromSeconds(10)),
            RelativeTime.Describe(TimeSpan.FromMinutes(5)),
            RelativeTime.Describe(TimeSpan.FromHours(3)),
            RelativeTime.Describe(TimeSpan.FromDays(2)),
        };

        Assert.Equal(["刚刚", "5 分钟前", "3 小时前", "2 天前"], labels);
        Assert.Equal(4, labels.Distinct().Count());
    }

    [Theory]
    [InlineData(0, "0 分钟")]
    [InlineData(-60, "0 分钟")]
    [InlineData(90, "2 分钟")]          // 1.5 minutes rounds up
    [InlineData(3600, "1 小时")]
    [InlineData(5400, "2 小时")]        // 1.5 hours rounds up
    [InlineData(86400, "1 天")]
    [InlineData(2340000, "28 天")]      // 27.08 days - must not read as "27"
    public void ACountdown_RoundsUp(int seconds, string expected)
    {
        Assert.Equal(expected, RelativeTime.DescribeUntil(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void StampForToday_IsJustTheClockTime()
    {
        var now = new DateTimeOffset(2026, 10, 7, 18, 0, 0, TimeSpan.FromHours(8));
        var moment = new DateTimeOffset(2026, 10, 7, 12, 34, 0, TimeSpan.FromHours(8));

        Assert.Equal("12:34", RelativeTime.Stamp(moment, now));
    }

    [Fact]
    public void StampForAnEarlierDay_CarriesTheDate()
    {
        // Otherwise "12:34" would be indistinguishable from today's 12:34.
        var now = new DateTimeOffset(2026, 10, 7, 18, 0, 0, TimeSpan.FromHours(8));
        var moment = new DateTimeOffset(2026, 10, 4, 12, 34, 0, TimeSpan.FromHours(8));

        Assert.Equal("10-04 12:34", RelativeTime.Stamp(moment, now));
    }
}
