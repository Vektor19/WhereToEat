namespace WhereToEat.Parsing.Integration.Fixtures;

/// <summary>
/// Resolves a pinned HTML fixture (copied next to the test assembly) to an absolute <c>file://</c>
/// <see cref="Uri"/> the AngleSharp strategy can fetch — CI-stable, never the live site.
/// </summary>
internal static class FixtureFile
{
    /// <summary>The <c>file://</c> URI of the fixture at <paramref name="relativePath"/> under Fixtures/.</summary>
    public static Uri Url(string relativePath)
    {
        var full = Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath);
        if (!File.Exists(full))
        {
            throw new FileNotFoundException($"Pinned fixture not found at '{full}'.", full);
        }

        return new Uri(full, UriKind.Absolute);
    }
}
