using System.Globalization;
using System.Net;
using System.Net.Http;
using KiwiTraffic.Infrastructure.KiwiVm;
using KiwiTraffic.Infrastructure.Tests.TestSupport;

namespace KiwiTraffic.Infrastructure.Tests;

public class KiwiVmClientTests
{
    private const string ApiKey = "private_example_key_do_not_log";
    private const long Veid = 12345L;
    private const string ProfileId = "veid:12345";

    private const string HealthyJson = """
        {
          "vm_type": "kvm",
          "hostname": "example.invalid",
          "plan": "bwg-20g",
          "plan_monthly_data": 1000000000000,
          "monthly_data_multiplier": 1,
          "data_counter": 250000000000,
          "data_next_reset": 1793491200,
          "error": 0
        }
        """;

    private static KiwiVmClient Build(StubHttpMessageHandler handler, TimeSpan? timeout = null)
    {
        var http = new HttpClient(handler, disposeHandler: false);
        return new KiwiVmClient(http, new UsageMapper(), TimeProvider.System, timeout);
    }

    private static Task<KiwiVmQueryResult> Query(
        StubHttpMessageHandler handler,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) =>
        Build(handler, timeout).GetServiceInfoAsync(Veid, ApiKey, ProfileId, cancellationToken);

    // --- what goes on the wire ------------------------------------------------

    [Fact]
    public async Task SendsParametersAsAPostBody_NotInTheUrl()
    {
        var handler = StubHttpMessageHandler.Json(HealthyJson);

        await Query(handler);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("application/x-www-form-urlencoded", request.ContentType);
        Assert.Contains($"veid={Veid}", request.Body);
        Assert.Contains($"api_key={ApiKey}", request.Body);

        // The whole point: credentials must never be part of a URL, a proxy log
        // or a server access log.
        Assert.DoesNotContain(ApiKey, request.Uri!.ToString());
        Assert.DoesNotContain("api_key", request.Uri.ToString());
    }

    [Fact]
    public async Task TalksOnlyToTheFixedHttpsEndpoint()
    {
        var handler = StubHttpMessageHandler.Json(HealthyJson);

        await Query(handler);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(KiwiVmClient.ServiceInfoEndpoint, request.Uri);
        Assert.Equal("https", request.Uri!.Scheme);
        Assert.Equal("api.64clouds.com", request.Uri.Host);
    }

    // --- outcomes -------------------------------------------------------------

    [Fact]
    public async Task HealthyResponse_ReturnsASnapshot()
    {
        var result = await Query(StubHttpMessageHandler.Json(HealthyJson));

        Assert.True(result.IsSuccess);
        Assert.Equal(250_000_000_000m, result.Snapshot!.UsedBytes);
        Assert.Equal(1_000_000_000_000m, result.Snapshot.QuotaBytes);
        Assert.Equal(25m, result.Snapshot.ToUsage().UsedPercent);
        Assert.Equal(ProfileId, result.Snapshot.ProfileId);

        // The reading is stamped with the moment it was fetched, in UTC.
        Assert.Equal(TimeSpan.Zero, result.Snapshot.FetchedAtUtc.Offset);
    }

    [Fact]
    public async Task BusinessErrorCarriedByHttp200_IsNotSuccess()
    {
        const string body = """{"error": 700005, "message": "Authentication failure"}""";

        var result = await Query(StubHttpMessageHandler.Json(body));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.Equal(KiwiVmErrorKind.Authentication, result.ErrorKind);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task HttpErrorStatus_IsANetworkFailure(HttpStatusCode status)
    {
        var result = await Query(StubHttpMessageHandler.Status(status));

        Assert.False(result.IsSuccess);
        Assert.Equal(KiwiVmErrorKind.Network, result.ErrorKind);
        Assert.Contains(((int)status).ToString(CultureInfo.InvariantCulture), result.ErrorMessage!);
    }

    [Fact]
    public async Task NonJsonBody_IsAnInvalidResponse()
    {
        var result = await Query(StubHttpMessageHandler.Json("<html>login required</html>"));

        Assert.Equal(KiwiVmErrorKind.InvalidResponse, result.ErrorKind);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task TransportFailure_IsReportedAsANetworkError()
    {
        var handler = StubHttpMessageHandler.Throws(new HttpRequestException("connection refused"));

        var result = await Query(handler);

        Assert.Equal(KiwiVmErrorKind.Network, result.ErrorKind);
        Assert.Contains("connection refused", result.ErrorMessage!);
    }

    [Fact]
    public async Task ExpiredRequest_IsReportedAsATimeout_NotAsACancellation()
    {
        var handler = StubHttpMessageHandler.Hangs();

        var result = await Query(handler, timeout: TimeSpan.FromMilliseconds(150));

        Assert.Equal(KiwiVmErrorKind.Network, result.ErrorKind);
        Assert.Contains("超时", result.ErrorMessage!);
    }

    [Fact]
    public async Task CallerCancellation_IsReportedAsCancelled()
    {
        var handler = StubHttpMessageHandler.Hangs();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await Query(handler, timeout: TimeSpan.FromSeconds(30), cancellationToken: cancellation.Token);

        Assert.Equal(KiwiVmErrorKind.Cancelled, result.ErrorKind);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task ErrorsNeverLeakTheApiKey()
    {
        var handler = StubHttpMessageHandler.Throws(new HttpRequestException($"failed posting to {KiwiVmClient.ServiceInfoEndpoint}"));

        var result = await Query(handler);

        Assert.DoesNotContain(ApiKey, result.ErrorMessage!);
    }
}
