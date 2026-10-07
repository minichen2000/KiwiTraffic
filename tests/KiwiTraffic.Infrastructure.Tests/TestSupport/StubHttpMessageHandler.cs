using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;

namespace KiwiTraffic.Infrastructure.Tests.TestSupport;

/// <summary>One captured request, with everything the test needs already copied out.</summary>
internal sealed record CapturedRequest(string Method, Uri? Uri, string? Body, string? ContentType);

/// <summary>
/// Replaces the network in client tests. Captures what was sent so tests can
/// assert on the request itself (for example that no credential reached the URL).
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

    private StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public List<CapturedRequest> Requests { get; } = [];

    public static StubHttpMessageHandler Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));

    public static StubHttpMessageHandler Status(HttpStatusCode status) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)));

    public static StubHttpMessageHandler Throws(Exception exception) =>
        new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    /// <summary>Never answers until cancelled - used to exercise timeouts.</summary>
    public static StubHttpMessageHandler Hangs() =>
        new(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new UnreachableException();
        });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        Requests.Add(new CapturedRequest(
            request.Method.Method,
            request.RequestUri,
            body,
            request.Content?.Headers.ContentType?.MediaType));

        return await _responder(request, cancellationToken).ConfigureAwait(false);
    }
}
