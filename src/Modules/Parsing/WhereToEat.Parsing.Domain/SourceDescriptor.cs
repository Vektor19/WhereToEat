namespace WhereToEat.Parsing.Domain;

/// <summary>
/// The input that selects and drives a parser run: the first-party <see cref="Url"/> to fetch, the
/// <see cref="StrategyKey"/> naming which <c>IParserStrategy</c> handles it (e.g. <c>"selenium"</c>,
/// <c>"anglesharp"</c> — resolved by key, invariant #4), and the <see cref="RestaurantName"/> the
/// source represents. The descriptor is the unit a parser strategy and the compliance gates both
/// operate on; the host/worker builds one per first-party source.
/// </summary>
public sealed record SourceDescriptor(string RestaurantName, Uri Url, string StrategyKey);
