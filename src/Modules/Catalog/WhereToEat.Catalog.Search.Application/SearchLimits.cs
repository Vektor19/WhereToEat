namespace WhereToEat.Catalog.Search.Application;

/// <summary>
/// Shared clamp for the search result caps so a caller can never request an unbounded scan and a
/// non-positive limit falls back to a sensible default (the search stays predictable — invariant #1).
/// </summary>
internal static class SearchLimits
{
    public static int Clamp(int requested, int @default, int max)
    {
        if (requested <= 0)
        {
            return @default;
        }

        return requested > max ? max : requested;
    }
}
