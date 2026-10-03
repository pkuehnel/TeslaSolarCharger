using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TeslaSolarCharger.Tests;

internal sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Body, string? Authorization = null);

/// <summary>
/// Answers HTTP requests with the given function and records them, so tests can check what was sent.
/// </summary>
internal sealed class CapturingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((request, _) => Task.FromResult(respond(request)))
    {
    }

    public List<CapturedRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body, request.Headers.Authorization?.ToString()));
        return await respond(request, cancellationToken);
    }
}
