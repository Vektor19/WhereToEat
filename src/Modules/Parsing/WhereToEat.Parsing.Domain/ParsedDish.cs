using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Parsing.Domain;

/// <summary>
/// One raw menu line a parser extracted from a first-party source, <b>before</b> normalization: the
/// <see cref="RawName"/> exactly as it appeared on the page, the parsed <see cref="Price"/> as a
/// <see cref="Money"/> value object (units/currency already unified by the strategy), an optional
/// free-form <see cref="Weight"/> label (e.g. "300 г"), and an optional <see cref="CategoryHint"/>
/// (the page's own grouping heading, used only as a hint for normalization — never as proof of a
/// canonical match). The normalizer maps <see cref="RawName"/> onto the canonical taxonomy; an
/// unmappable name becomes a quarantine item rather than being dropped (invariant #2).
/// </summary>
public sealed record ParsedDish(string RawName, Money Price, string? Weight, string? CategoryHint);
