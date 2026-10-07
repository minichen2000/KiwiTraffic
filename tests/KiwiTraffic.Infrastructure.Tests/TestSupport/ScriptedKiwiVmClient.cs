using KiwiTraffic.Core.Model;
using KiwiTraffic.Infrastructure.KiwiVm;

namespace KiwiTraffic.Infrastructure.Tests.TestSupport;

/// <summary>
/// An <see cref="IKiwiVmClient"/> whose answers the test decides, so scenarios
/// like "the response for the old account arrives after the switch" can be
/// driven exactly.
/// </summary>
internal sealed class ScriptedKiwiVmClient : IKiwiVmClient
{
    private readonly Func<string, CancellationToken, Task<KiwiVmQueryResult>> _responder;

    public ScriptedKiwiVmClient(Func<string, CancellationToken, Task<KiwiVmQueryResult>> responder)
    {
        _responder = responder;
    }

    public List<string> RequestedProfiles { get; } = [];

    public Task<KiwiVmQueryResult> GetServiceInfoAsync(
        long veid,
        string apiKey,
        string profileId,
        CancellationToken cancellationToken = default)
    {
        RequestedProfiles.Add(profileId);
        return _responder(profileId, cancellationToken);
    }

    public static TrafficSnapshot SnapshotFor(string profileId, decimal usedBytes = 100m) => new()
    {
        ProfileId = profileId,
        Veid = 1,
        UsedBytes = usedBytes,
        QuotaBytes = 1000m,
        NextResetAtUtc = null,
        FetchedAtUtc = DateTimeOffset.UnixEpoch,
    };
}
