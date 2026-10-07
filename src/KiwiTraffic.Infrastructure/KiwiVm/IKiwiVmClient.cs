namespace KiwiTraffic.Infrastructure.KiwiVm;

/// <summary>
/// Queries the KiwiVM API. Implementations never touch the UI and never log
/// or echo credentials.
/// </summary>
public interface IKiwiVmClient
{
    /// <summary>
    /// Fetches the service info for one VPS.
    /// </summary>
    /// <param name="veid">The VEID to query.</param>
    /// <param name="apiKey">
    /// The API key. Passed as a plain argument on purpose: putting it into a
    /// record or wrapper type would give it a generated <c>ToString()</c> that
    /// leaks the key the first time anything logs that object.
    /// </param>
    /// <param name="profileId">Identity of the configuration this reading belongs to.</param>
    /// <param name="cancellationToken">Cancels the request; a cancelled call never yields a snapshot.</param>
    Task<KiwiVmQueryResult> GetServiceInfoAsync(
        long veid,
        string apiKey,
        string profileId,
        CancellationToken cancellationToken = default);
}
