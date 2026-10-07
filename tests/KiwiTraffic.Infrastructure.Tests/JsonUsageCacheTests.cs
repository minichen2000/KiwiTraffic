using KiwiTraffic.Core.Cache;
using KiwiTraffic.Core.Model;
using KiwiTraffic.Infrastructure.Cache;
using KiwiTraffic.Infrastructure.Tests.TestSupport;

namespace KiwiTraffic.Infrastructure.Tests;

public class JsonUsageCacheTests
{
    private static CachedUsage Entry(string profileId = "veid:12345") => new()
    {
        ProfileId = profileId,
        Snapshot = new TrafficSnapshot
        {
            ProfileId = profileId,
            Veid = 12345,
            UsedBytes = 250m,
            QuotaBytes = 1000m,
            NextResetAtUtc = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            FetchedAtUtc = new DateTimeOffset(2026, 10, 7, 8, 0, 0, TimeSpan.Zero),
        },
    };

    [Fact]
    public async Task NoFileYet_LoadsAsNull()
    {
        using var temp = new TempDirectory();

        Assert.Null(await new JsonUsageCache(temp.ToAppPaths()).LoadAsync());
    }

    [Fact]
    public async Task SaveThenLoad_RoundTrips()
    {
        using var temp = new TempDirectory();
        var cache = new JsonUsageCache(temp.ToAppPaths());

        await cache.SaveAsync(Entry());
        var loaded = await cache.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("veid:12345", loaded.ProfileId);
        Assert.Equal(250m, loaded.Snapshot.UsedBytes);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), loaded.Snapshot.NextResetAtUtc);
    }

    [Fact]
    public async Task CachedReading_IsBoundToItsProfile()
    {
        using var temp = new TempDirectory();
        var cache = new JsonUsageCache(temp.ToAppPaths());
        await cache.SaveAsync(Entry("veid:12345"));

        var loaded = await cache.LoadAsync();

        var sameProfile = new Core.Settings.AppSettings { Veid = 12345 };
        var otherProfile = new Core.Settings.AppSettings { Veid = 67890 };

        Assert.True(loaded!.BelongsTo(sameProfile));
        Assert.False(loaded.BelongsTo(otherProfile));
    }

    [Fact]
    public async Task BrokenCache_IsDiscarded_NotSurfacedAsAnError()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(temp.File("cache.json"), "{ broken");

        var loaded = await new JsonUsageCache(temp.ToAppPaths()).LoadAsync();

        Assert.Null(loaded);
        Assert.Contains(temp.FileNames(), name => name.Contains("corrupt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CacheWithoutAProfileBinding_IsDiscarded()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(temp.File("cache.json"), """{"schemaVersion": 1, "profileId": ""}""");

        Assert.Null(await new JsonUsageCache(temp.ToAppPaths()).LoadAsync());
    }

    [Fact]
    public async Task CacheFromANewerSchema_IsDiscarded()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(temp.File("cache.json"), """{"schemaVersion": 99, "profileId": "veid:1"}""");

        Assert.Null(await new JsonUsageCache(temp.ToAppPaths()).LoadAsync());
    }
}
