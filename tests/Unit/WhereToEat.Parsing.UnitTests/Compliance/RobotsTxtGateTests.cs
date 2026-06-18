using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WhereToEat.Parsing.Infrastructure.Compliance;
using Xunit;

namespace WhereToEat.Parsing.UnitTests.Compliance;

/// <summary>
/// The robots.txt gate (invariant #9), tested against a fake <see cref="HttpMessageHandler"/> serving a
/// pinned robots.txt body (no live calls): a <c>Disallow</c> covering the target path blocks the parse;
/// an unrelated rule allows it; a 404 (no robots.txt) allows everything; and a network error is treated
/// conservatively as a disallow ("we do not fetch a site we could not check").
/// </summary>
public sealed class RobotsTxtGateTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private static RobotsTxtGate Build(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new(new HttpClient(new StubHandler(responder)), NullLogger<RobotsTxtGate>.Instance);

    private static HttpResponseMessage Ok(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public async Task Disallow_CoveringThePath_BlocksTheParse()
    {
        var gate = Build(_ => Ok("User-agent: *\nDisallow: /menu"));

        (await gate.IsAllowedAsync(new Uri("https://borsch-cafe.example/menu")))
            .Should().BeFalse("robots.txt disallows /menu");
    }

    [Fact]
    public async Task UnrelatedDisallow_AllowsThePath()
    {
        var gate = Build(_ => Ok("User-agent: *\nDisallow: /admin"));

        (await gate.IsAllowedAsync(new Uri("https://borsch-cafe.example/menu")))
            .Should().BeTrue("only /admin is disallowed");
    }

    [Fact]
    public async Task NoRobotsTxt_404_AllowsEverything()
    {
        var gate = Build(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        (await gate.IsAllowedAsync(new Uri("https://borsch-cafe.example/menu")))
            .Should().BeTrue("the standard treats a missing robots.txt as no restrictions");
    }

    [Fact]
    public async Task NetworkError_IsTreatedAsDisallow()
    {
        var gate = Build(_ => throw new HttpRequestException("network down"));

        (await gate.IsAllowedAsync(new Uri("https://borsch-cafe.example/menu")))
            .Should().BeFalse("a site we could not check is conservatively not fetched (invariant #9)");
    }
}
