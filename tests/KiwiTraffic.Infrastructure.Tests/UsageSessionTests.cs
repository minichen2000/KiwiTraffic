using System.Diagnostics;
using KiwiTraffic.Core.Settings;
using KiwiTraffic.Infrastructure.Cache;
using KiwiTraffic.Infrastructure.KiwiVm;
using KiwiTraffic.Infrastructure.Tests.TestSupport;

namespace KiwiTraffic.Infrastructure.Tests;

public class UsageSessionTests
{
    private static AppSettings SettingsFor(long veid, string alias = "") => new() { Veid = veid, Alias = alias };

    private sealed class SessionFixture : IDisposable
    {
        public SessionFixture(Func<string, CancellationToken, Task<KiwiVmQueryResult>> responder)
        {
            Temp = new TempDirectory();
            Client = new ScriptedKiwiVmClient(responder);
            Cache = new JsonUsageCache(Temp.ToAppPaths());
            Session = new UsageSession(Client, Cache);
        }

        public TempDirectory Temp { get; }

        public ScriptedKiwiVmClient Client { get; }

        public JsonUsageCache Cache { get; }

        public UsageSession Session { get; }

        public void Dispose()
        {
            Session.Dispose();
            Temp.Dispose();
        }
    }

    [Fact]
    public async Task Unconfigured_ReportsThatNothingIsSetUp_WithoutCallingOut()
    {
        using var fixture = new SessionFixture((_, _) => throw new UnreachableException());
        fixture.Session.Configure(new AppSettings(), apiKey: null);

        var result = await fixture.Session.RefreshAsync();

        Assert.False(result.IsSuccess);
        Assert.Empty(fixture.Client.RequestedProfiles);
    }

    [Fact]
    public async Task SuccessfulReading_IsReturnedAndCached()
    {
        using var fixture = new SessionFixture((profileId, _) =>
            Task.FromResult(KiwiVmQueryResult.Success(ScriptedKiwiVmClient.SnapshotFor(profileId))));

        fixture.Session.Configure(SettingsFor(1), "private_key");
        var result = await fixture.Session.RefreshAsync();

        Assert.True(result.IsSuccess);

        var cached = await fixture.Cache.LoadAsync();
        Assert.Equal("veid:1", cached!.ProfileId);
        Assert.Equal(100m, cached.Snapshot.UsedBytes);
    }

    [Fact]
    public async Task ConcurrentRefreshes_ShareOneRequest()
    {
        var release = new TaskCompletionSource<KiwiVmQueryResult>();
        using var fixture = new SessionFixture((_, _) => release.Task);

        fixture.Session.Configure(SettingsFor(1), "private_key");
        var first = fixture.Session.RefreshAsync();
        var second = fixture.Session.RefreshAsync();
        var third = fixture.Session.RefreshAsync();

        release.SetResult(KiwiVmQueryResult.Success(ScriptedKiwiVmClient.SnapshotFor("veid:1")));
        var results = await Task.WhenAll(first, second, third);

        Assert.Single(fixture.Client.RequestedProfiles);
        Assert.All(results, result => Assert.True(result.IsSuccess));
    }

    [Fact]
    public async Task SwitchingConfiguration_DiscardsTheOldResponse_EvenIfItArrivesLate()
    {
        var oldRequestStarted = new TaskCompletionSource();
        var oldResponse = new TaskCompletionSource<KiwiVmQueryResult>();

        using var fixture = new SessionFixture((profileId, _) =>
        {
            if (profileId == "veid:1")
            {
                oldRequestStarted.SetResult();
                return oldResponse.Task; // deliberately ignores cancellation
            }

            return Task.FromResult(KiwiVmQueryResult.Success(
                ScriptedKiwiVmClient.SnapshotFor(profileId, usedBytes: 500m)));
        });

        fixture.Session.Configure(SettingsFor(1, "旧机器"), "private_key_old");
        var oldTask = fixture.Session.RefreshAsync();
        await oldRequestStarted.Task;

        // The user switches to a different VPS while the first call is running.
        fixture.Session.Configure(SettingsFor(2, "新机器"), "private_key_new");
        var newResult = await fixture.Session.RefreshAsync();

        Assert.True(newResult.IsSuccess);
        Assert.Equal("veid:2", newResult.Snapshot!.ProfileId);

        // The old call now finally answers.
        oldResponse.SetResult(KiwiVmQueryResult.Success(
            ScriptedKiwiVmClient.SnapshotFor("veid:1", usedBytes: 999m)));
        var oldResult = await oldTask;

        Assert.False(oldResult.IsSuccess);
        Assert.Null(oldResult.Snapshot);

        // ... and it must not have overwritten the new machine's cache either.
        var cached = await fixture.Cache.LoadAsync();
        Assert.Equal("veid:2", cached!.ProfileId);
        Assert.Equal(500m, cached.Snapshot.UsedBytes);
    }

    [Fact]
    public async Task ResponseForADifferentProfile_IsRejected()
    {
        // Defence in depth: whatever the transport does, a reading is only
        // accepted for the profile that asked for it.
        using var fixture = new SessionFixture((_, _) =>
            Task.FromResult(KiwiVmQueryResult.Success(ScriptedKiwiVmClient.SnapshotFor("veid:999"))));

        fixture.Session.Configure(SettingsFor(1), "private_key");
        var result = await fixture.Session.RefreshAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(KiwiVmErrorKind.InvalidResponse, result.ErrorKind);
        Assert.Null(await fixture.Cache.LoadAsync());
    }

    [Fact]
    public async Task FailedReading_IsNeverCached()
    {
        using var fixture = new SessionFixture((_, _) =>
            Task.FromResult(KiwiVmQueryResult.Failure(KiwiVmErrorKind.Network, "网络错误")));

        fixture.Session.Configure(SettingsFor(1), "private_key");
        var result = await fixture.Session.RefreshAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(await fixture.Cache.LoadAsync());
    }

    [Fact]
    public async Task CachedReadingFromAnotherProfile_IsNotOffered()
    {
        using var fixture = new SessionFixture((profileId, _) =>
            Task.FromResult(KiwiVmQueryResult.Success(ScriptedKiwiVmClient.SnapshotFor(profileId))));

        fixture.Session.Configure(SettingsFor(1), "private_key");
        await fixture.Session.RefreshAsync();

        // Same cache file, different VPS: the old reading must not be shown.
        fixture.Session.Configure(SettingsFor(2), "private_key");

        Assert.Null(await fixture.Session.LoadCachedAsync());
    }

    [Fact]
    public async Task CachedReadingForTheCurrentProfile_IsOffered()
    {
        using var fixture = new SessionFixture((profileId, _) =>
            Task.FromResult(KiwiVmQueryResult.Success(ScriptedKiwiVmClient.SnapshotFor(profileId))));

        fixture.Session.Configure(SettingsFor(1), "private_key");
        await fixture.Session.RefreshAsync();

        var cached = await fixture.Session.LoadCachedAsync();

        Assert.Equal("veid:1", cached!.ProfileId);
    }

    [Fact]
    public async Task AfterDispose_RefreshingIsRejected()
    {
        using var fixture = new SessionFixture((_, _) => throw new UnreachableException());

        fixture.Session.Configure(SettingsFor(1), "private_key");
        fixture.Session.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Session.RefreshAsync());
    }
}
