using System.Net;
using Microsoft.Extensions.Logging;
using WhereToEat.Parsing.Application.Compliance;

namespace WhereToEat.Parsing.Infrastructure.Compliance;

/// <summary>
/// The robots.txt compliance gate (invariant #9). Before any menu fetch it retrieves the host's
/// <c>/robots.txt</c> over a typed <see cref="HttpClient"/> and evaluates the <c>Disallow</c> rules
/// of the wildcard <c>*</c> group (our parser does not claim a named agent — see
/// <c>RobotsTxtPolicy</c>) against the target path. The longest-matching <c>Allow</c>/<c>Disallow</c>
/// rule wins, per the de-facto robots standard.
///
/// <b>Conservative on uncertainty:</b> a network error retrieving robots.txt is treated as
/// <b>disallow</b> (we do not fetch a site we could not check), while an explicit <c>404</c> (no
/// robots.txt at all) means "no restrictions" and is allowed — the standard behaviour.
/// </summary>
public sealed partial class RobotsTxtGate : IRobotsTxtGate
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RobotsTxtGate> _logger;

    public RobotsTxtGate(HttpClient httpClient, ILogger<RobotsTxtGate> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsAllowedAsync(Uri url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        var robotsUri = new Uri(url, "/robots.txt");
        string robotsBody;
        try
        {
            using var response = await _httpClient.GetAsync(robotsUri, cancellationToken).ConfigureAwait(false);

            // No robots.txt (404) -> the standard says everything is allowed.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return true;
            }

            if (!response.IsSuccessStatusCode)
            {
                LogUnavailable(robotsUri, (int)response.StatusCode);
                return false;
            }

            robotsBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            // Could not check -> do not fetch (be conservative, invariant #9).
            LogFetchError(robotsUri, ex.Message);
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogFetchError(robotsUri, "timeout");
            return false;
        }

        return RobotsTxtPolicy.IsAllowed(robotsBody, url.AbsolutePath);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "robots.txt at {RobotsUri} returned status {StatusCode}; treating as disallow (conservative).")]
    private partial void LogUnavailable(Uri robotsUri, int statusCode);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "robots.txt at {RobotsUri} could not be retrieved ({Reason}); treating as disallow (conservative).")]
    private partial void LogFetchError(Uri robotsUri, string reason);
}
