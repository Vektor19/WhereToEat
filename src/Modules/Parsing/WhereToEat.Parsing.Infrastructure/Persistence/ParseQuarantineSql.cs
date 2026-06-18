namespace WhereToEat.Parsing.Infrastructure.Persistence;

/// <summary>
/// The hand-written SQL for the parse quarantine queue (invariant #2), kept in one place as named
/// constants so the statements are reviewable as a unit. The store only ever appends unmappable items
/// (an admin later resolves/removes them), so a single parameterised <c>INSERT</c> is all it needs.
/// </summary>
internal static class ParseQuarantineSql
{
    internal const string Insert =
        """
        INSERT INTO parsing.ParseQuarantine
            (Id, RestaurantName, RestaurantAddressLine, RawName, PriceAmount, PriceCurrency, Weight, CategoryHint, DetectedAtUtc)
        VALUES
            (@Id, @RestaurantName, @RestaurantAddressLine, @RawName, @PriceAmount, @PriceCurrency, @Weight, @CategoryHint, @DetectedAtUtc);
        """;
}
