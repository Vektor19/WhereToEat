using System.Net;

namespace WhereToEat.Geo.UnitTests;

/// <summary>
/// A test <see cref="HttpMessageHandler"/> that serves a canned response and records how many
/// requests it saw, each request's outbound <c>User-Agent</c>, and the <see cref="TimeProvider"/>
/// timestamp at which it was invoked. No network — this is the "pinned fixture, no live Nominatim"
/// substitute the CI policy requires; the timestamps prove the geocoder honours its rate-limit
/// interval between sends.
/// </summary>
public sealed class CountingMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    private readonly TimeProvider _timeProvider;
    private readonly List<long> _requestTimestamps = new();
    private readonly List<string?> _userAgents = new();

    public CountingMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        TimeProvider? timeProvider = null)
    {
        _responder = responder;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>A handler that always returns 200 with <paramref name="json"/> as the body.</summary>
    public static CountingMessageHandler RespondingWith(string json, TimeProvider? timeProvider = null)
        => new(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            },
            timeProvider);

    /// <summary>The number of requests the geocoder issued.</summary>
    public int RequestCount => _requestTimestamps.Count;

    /// <summary>The recorded <see cref="TimeProvider"/> timestamps (one per request).</summary>
    public IReadOnlyList<long> RequestTimestamps => _requestTimestamps;

    /// <summary>The <c>User-Agent</c> header value seen on each request.</summary>
    public IReadOnlyList<string?> UserAgents => _userAgents;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requestTimestamps.Add(_timeProvider.GetTimestamp());
        _userAgents.Add(request.Headers.UserAgent.ToString());
        return Task.FromResult(_responder(request));
    }
}
