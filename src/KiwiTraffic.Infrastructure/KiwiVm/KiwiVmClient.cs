using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace KiwiTraffic.Infrastructure.KiwiVm;

/// <summary>
/// HTTP implementation of <see cref="IKiwiVmClient"/>.
/// </summary>
/// <remarks>
/// The endpoint is a compile-time constant: the project deliberately does not
/// accept a user-supplied base URL, so a credential can never be posted
/// somewhere else. Parameters travel in the request body, so the API key never
/// appears in a URL, a proxy log or a server access log.
/// </remarks>
public sealed class KiwiVmClient : IKiwiVmClient
{
    /// <summary>The only host and path this client will ever talk to.</summary>
    public static readonly Uri ServiceInfoEndpoint = new("https://api.64clouds.com/v1/getServiceInfo");

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient _httpClient;
    private readonly UsageMapper _mapper;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _timeout;

    public KiwiVmClient(
        HttpClient httpClient,
        UsageMapper mapper,
        TimeProvider? timeProvider = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(mapper);

        _httpClient = httpClient;
        _mapper = mapper;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _timeout = timeout ?? DefaultTimeout;
    }

    public async Task<KiwiVmQueryResult> GetServiceInfoAsync(
        long veid,
        string apiKey,
        string profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        // A linked source lets us tell "the caller cancelled" apart from
        // "the request timed out", which need different UI treatment.
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        try
        {
            using var form = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("veid", veid.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("api_key", apiKey),
            ]);

            using var request = new HttpRequestMessage(HttpMethod.Post, ServiceInfoEndpoint)
            {
                Content = form,
            };

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutSource.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return KiwiVmQueryResult.Failure(
                    KiwiVmErrorKind.Network,
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd());
            }

            var json = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);

            ServiceInfoResponse? payload;
            try
            {
                payload = JsonSerializer.Deserialize<ServiceInfoResponse>(json, KiwiVmJson.Options);
            }
            catch (JsonException ex)
            {
                return KiwiVmQueryResult.Failure(
                    KiwiVmErrorKind.InvalidResponse,
                    $"响应不是预期的 JSON（{ex.Message}）。");
            }

            if (payload is null)
            {
                return KiwiVmQueryResult.Failure(KiwiVmErrorKind.InvalidResponse, "响应内容为空。");
            }

            return _mapper.Map(payload, profileId, veid, _timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return KiwiVmQueryResult.Failure(
                KiwiVmErrorKind.Network,
                $"请求超时（{_timeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} 秒）。");
        }
        catch (OperationCanceledException)
        {
            return KiwiVmQueryResult.Failure(KiwiVmErrorKind.Cancelled, "请求已取消。");
        }
        catch (HttpRequestException ex)
        {
            // The message describes DNS/TLS/proxy trouble. It cannot contain the
            // API key: parameters are in the body, and no redirect is followed.
            return KiwiVmQueryResult.Failure(KiwiVmErrorKind.Network, $"网络错误：{ex.Message}");
        }
    }
}
