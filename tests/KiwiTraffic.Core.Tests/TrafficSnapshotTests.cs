using KiwiTraffic.Core.Model;

namespace KiwiTraffic.Core.Tests;

public class TrafficSnapshotTests
{
    [Fact]
    public void ToUsage_UsesTheSnapshotCounts()
    {
        var snapshot = new TrafficSnapshot
        {
            ProfileId = "profile-1",
            Veid = 12345,
            UsedBytes = 250m,
            QuotaBytes = 1000m,
            NextResetAtUtc = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            FetchedAtUtc = new DateTimeOffset(2026, 10, 7, 4, 30, 0, TimeSpan.Zero),
        };

        var usage = snapshot.ToUsage();

        Assert.Equal(25m, usage.UsedPercent);
        Assert.Equal(750m, usage.RemainingBytes);
    }

    [Fact]
    public void SnapshotWithoutQuota_ProducesUnknownUsage_NotZero()
    {
        var snapshot = new TrafficSnapshot
        {
            ProfileId = "profile-1",
            Veid = 12345,
            UsedBytes = 250m,
            QuotaBytes = null,
            NextResetAtUtc = null,
            FetchedAtUtc = DateTimeOffset.UnixEpoch,
        };

        var usage = snapshot.ToUsage();

        Assert.False(usage.IsQuotaKnown);
        Assert.Null(usage.UsedPercent);
        Assert.Null(snapshot.NextResetAtUtc);
    }
}
