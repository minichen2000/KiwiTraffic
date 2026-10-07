using KiwiTraffic.Core.Cache;
using KiwiTraffic.Core.Settings;
using KiwiTraffic.Infrastructure.Cache;
using KiwiTraffic.Infrastructure.KiwiVm;

namespace KiwiTraffic.Infrastructure;

/// <summary>
/// Owns "the query that is currently allowed to happen".
/// </summary>
/// <remarks>
/// Guarantees, in the order the plan requires them:
/// <list type="bullet">
/// <item>at most one request is in flight at a time - concurrent callers join
/// the running request instead of starting another one;</item>
/// <item>switching configuration cancels the old request, and its response is
/// discarded even if it still arrives, so one VPS's numbers can never be shown
/// as another's;</item>
/// <item>only a successful, fully validated reading reaches the cache.</item>
/// </list>
/// Throttling, back-off and rate-limit cooldowns are deliberately not here -
/// they belong to the refresh coordinator (M4).
/// </remarks>
public sealed class UsageSession : IDisposable
{
    private readonly IKiwiVmClient _client;
    private readonly IUsageCache _cache;
    private readonly Lock _gate = new();

    private AppSettings? _settings;
    private string? _apiKey;
    private long _generation;
    private CancellationTokenSource? _sessionCancellation;
    private Task<KiwiVmQueryResult>? _inFlight;
    private bool _disposed;

    public UsageSession(IKiwiVmClient client, IUsageCache cache)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(cache);

        _client = client;
        _cache = cache;
    }

    /// <summary>The configuration currently in effect, or <c>null</c> when unconfigured.</summary>
    public AppSettings? CurrentSettings
    {
        get
        {
            lock (_gate)
            {
                return _settings;
            }
        }
    }

    /// <summary>
    /// Points the session at a configuration. Any request still running for the
    /// previous one is cancelled and can no longer be published.
    /// </summary>
    public void Configure(AppSettings settings, string? apiKey)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _generation++;
            _settings = settings;
            _apiKey = apiKey;
            _inFlight = null;

            _sessionCancellation?.Cancel();
            _sessionCancellation?.Dispose();
            _sessionCancellation = new CancellationTokenSource();
        }
    }

    /// <summary>
    /// Fetches the current reading. While a request is running, further calls
    /// share it rather than issuing another one.
    /// </summary>
    public Task<KiwiVmQueryResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_settings is not { } settings || string.IsNullOrEmpty(_apiKey))
            {
                return Task.FromResult(KiwiVmQueryResult.Failure(
                    KiwiVmErrorKind.Business,
                    "尚未配置 VEID 或 API Key。"));
            }

            if (_inFlight is { IsCompleted: false } running)
            {
                return running;
            }

            var generation = _generation;
            var sessionToken = _sessionCancellation?.Token ?? CancellationToken.None;

            _inFlight = ExecuteAsync(settings, _apiKey, generation, sessionToken, cancellationToken);
            return _inFlight;
        }
    }

    /// <summary>
    /// The last stored reading, or <c>null</c> when there is none - or when it
    /// belongs to a different configuration.
    /// </summary>
    public async Task<CachedUsage?> LoadCachedAsync(CancellationToken cancellationToken = default)
    {
        AppSettings? settings;
        lock (_gate)
        {
            settings = _settings;
        }

        if (settings is null)
        {
            return null;
        }

        var cached = await _cache.LoadAsync(cancellationToken).ConfigureAwait(false);

        return cached is not null && cached.BelongsTo(settings) ? cached : null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _sessionCancellation?.Cancel();
            _sessionCancellation?.Dispose();
            _sessionCancellation = null;
            _inFlight = null;
        }
    }

    private async Task<KiwiVmQueryResult> ExecuteAsync(
        AppSettings settings,
        string apiKey,
        long generation,
        CancellationToken sessionToken,
        CancellationToken callerToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(sessionToken, callerToken);

        var profileId = settings.ProfileId;
        var result = await _client
            .GetServiceInfoAsync(settings.Veid, apiKey, profileId, linked.Token)
            .ConfigureAwait(false);

        lock (_gate)
        {
            if (generation != _generation)
            {
                // The configuration changed while this was in flight. The
                // reading belongs to a machine the user is no longer looking
                // at, so it must not be cached or shown.
                return KiwiVmQueryResult.Failure(
                    KiwiVmErrorKind.Cancelled,
                    "配置已切换，本次结果已丢弃。");
            }
        }

        // Belt and braces: the response is only valid for the profile it was
        // requested for.
        if (result.Snapshot is { } snapshot &&
            !string.Equals(snapshot.ProfileId, profileId, StringComparison.Ordinal))
        {
            return KiwiVmQueryResult.Failure(
                KiwiVmErrorKind.InvalidResponse,
                "响应与当前配置不匹配，已丢弃。");
        }

        if (result.IsSuccess)
        {
            await _cache
                .SaveAsync(new CachedUsage { ProfileId = profileId, Snapshot = result.Snapshot! }, sessionToken)
                .ConfigureAwait(false);
        }

        return result;
    }
}
