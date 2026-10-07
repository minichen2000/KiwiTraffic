using System.Net;
using System.Net.Http;
using KiwiTraffic.Core.Settings;

namespace KiwiTraffic.Infrastructure.Networking;

/// <summary>
/// Builds the single <see cref="HttpClient"/> used for KiwiVM queries.
/// </summary>
public static class KiwiVmHttpClientFactory
{
    public static HttpClient Create(ProxySettings proxySettings, string? proxyPassword)
    {
        ArgumentNullException.ThrowIfNull(proxySettings);

        var handler = new HttpClientHandler
        {
            // The request body carries the API key. Following a redirect would
            // replay it to whatever host the redirect names.
            AllowAutoRedirect = false,

            // Certificate validation is left at its default (enabled). There is
            // no opt-out in this application.
        };

        switch (proxySettings.Mode)
        {
            case ProxyMode.Direct:
                handler.UseProxy = false;
                break;

            case ProxyMode.Manual:
                handler.UseProxy = true;
                handler.Proxy = BuildManualProxy(proxySettings, proxyPassword);
                break;

            case ProxyMode.System:
            default:
                // HttpClient.DefaultProxy resolves the .NET/WinHTTP system
                // settings. This is *not* guaranteed to match whatever a
                // particular browser extension does, and the UI must not
                // promise that it does.
                handler.UseProxy = true;
                break;
        }

        var client = new HttpClient(handler, disposeHandler: true)
        {
            // Timeouts are enforced per request by KiwiVmClient so that an
            // expiry can be reported as "timed out" rather than as a generic
            // cancellation. A second, invisible timeout here would only make
            // the failure harder to explain.
            Timeout = Timeout.InfiniteTimeSpan,
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"KiwiTraffic/{typeof(KiwiVmHttpClientFactory).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}");

        return client;
    }

    private static WebProxy BuildManualProxy(ProxySettings proxySettings, string? proxyPassword)
    {
        var address = new Uri($"http://{proxySettings.Host}:{proxySettings.Port}");
        var proxy = new WebProxy(address);

        if (!string.IsNullOrEmpty(proxySettings.Username) || !string.IsNullOrEmpty(proxyPassword))
        {
            // The password lives only in the encrypted credential file and is
            // never baked into the proxy URL.
            proxy.Credentials = new NetworkCredential(proxySettings.Username ?? string.Empty, proxyPassword ?? string.Empty);
        }

        return proxy;
    }
}
