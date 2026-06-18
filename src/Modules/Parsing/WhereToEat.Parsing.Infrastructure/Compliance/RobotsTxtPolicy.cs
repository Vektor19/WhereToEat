namespace WhereToEat.Parsing.Infrastructure.Compliance;

/// <summary>
/// A small, pure <c>robots.txt</c> evaluator (no I/O — unit-testable). It parses the <c>User-agent</c>
/// groups and their <c>Allow</c>/<c>Disallow</c> rules and decides whether a path is fetchable. We
/// evaluate against the wildcard <c>*</c> group (our parser does not claim a named agent), and apply
/// the de-facto standard: an empty <c>Disallow:</c> allows everything, and on a conflict the
/// <b>longest matching rule</b> wins (an <c>Allow</c> of equal length beats a <c>Disallow</c>).
/// </summary>
internal static class RobotsTxtPolicy
{
    /// <summary>
    /// Returns whether <paramref name="path"/> is allowed by <paramref name="robotsBody"/> for the
    /// wildcard agent group. A body with no applicable rules allows everything.
    /// </summary>
    public static bool IsAllowed(string robotsBody, string path)
    {
        if (string.IsNullOrWhiteSpace(robotsBody))
        {
            return true;
        }

        var inWildcardGroup = false;
        string? bestRulePath = null;
        var bestRuleAllows = true;

        foreach (var rawLine in robotsBody.Split('\n'))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            var field = line[..separator].Trim().ToLowerInvariant();
            var value = line[(separator + 1)..].Trim();

            switch (field)
            {
                case "user-agent":
                    // A new group header; we only care about the wildcard group.
                    inWildcardGroup = value == "*";
                    break;

                case "disallow" when inWildcardGroup:
                    // An empty Disallow means "allow all" — represented as a zero-length rule that
                    // never out-matches a real path prefix, so it has no blocking effect.
                    if (value.Length > 0 && PathMatches(path, value) && value.Length > (bestRulePath?.Length ?? -1))
                    {
                        bestRulePath = value;
                        bestRuleAllows = false;
                    }

                    break;

                case "allow" when inWildcardGroup:
                    // An Allow of length >= the current best wins (ties favour Allow).
                    if (value.Length > 0 && PathMatches(path, value) && value.Length >= (bestRulePath?.Length ?? -1))
                    {
                        bestRulePath = value;
                        bestRuleAllows = true;
                    }

                    break;

                default:
                    break;
            }
        }

        return bestRulePath is null || bestRuleAllows;
    }

    private static bool PathMatches(string path, string rulePath)
        => path.StartsWith(rulePath, StringComparison.Ordinal);

    private static string StripComment(string line)
    {
        var hash = line.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? line : line[..hash];
    }
}
