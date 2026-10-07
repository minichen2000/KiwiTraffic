using System.Net.Http;
using KiwiTraffic.Core.Settings;
using KiwiTraffic.Infrastructure;
using KiwiTraffic.Infrastructure.Cache;
using KiwiTraffic.Infrastructure.Configuration;
using KiwiTraffic.Infrastructure.Credentials;
using KiwiTraffic.Infrastructure.KiwiVm;
using KiwiTraffic.Infrastructure.Networking;

namespace KiwiTraffic.App.Services;

/// <summary>
/// Composition root. Deliberately hand-written: the plan rules out a DI
/// framework, and there are only a handful of objects.
/// </summary>
public sealed class AppServices : IDisposable
{
    private HttpClient? _httpClient;
    private bool _disposed;

    public AppServices()
    {
        Paths = new AppPaths();
        Paths.EnsureCreated();

        SettingsStore = new JsonSettingsStore(Paths);
        CredentialStore = new DpapiCredentialStore(Paths);
        Cache = new JsonUsageCache(Paths);
    }

    public AppPaths Paths { get; }

    public ISettingsStore SettingsStore { get; }

    public ICredentialStore CredentialStore { get; }

    public IUsageCache Cache { get; }

    public UsageSession? Session { get; private set; }

    /// <summary>
    /// (Re)builds the session for a configuration. The previous HTTP client is
    /// disposed, which also aborts anything still in flight for the old one.
    /// </summary>
    public UsageSession ConfigureSession(AppSettings settings, string? apiKey, string? proxyPassword)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Session?.Dispose();
        _httpClient?.Dispose();

        _httpClient = KiwiVmHttpClientFactory.Create(settings.Proxy, proxyPassword);
        Session = new UsageSession(new KiwiVmClient(_httpClient, new UsageMapper()), Cache);
        Session.Configure(settings, apiKey);

        return Session;
    }

    /// <summary>
    /// Tries the credentials the user just typed, without touching the stored
    /// configuration - a failed test must not disturb a working setup.
    /// </summary>
    public static async Task<KiwiVmQueryResult> TestConnectionAsync(
        AppSettings probe,
        string apiKey,
        string? proxyPassword,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = KiwiVmHttpClientFactory.Create(probe.Proxy, proxyPassword);

        return await new KiwiVmClient(httpClient, new UsageMapper())
            .GetServiceInfoAsync(probe.Veid, apiKey, probe.ProfileId, cancellationToken)
            .ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Session?.Dispose();
        _httpClient?.Dispose();
    }
}
