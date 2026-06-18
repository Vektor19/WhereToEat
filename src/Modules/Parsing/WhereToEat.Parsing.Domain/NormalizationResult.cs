using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Parsing.Domain;

/// <summary>
/// One raw dish line successfully mapped to the canonical taxonomy (invariant #2): the canonical
/// <see cref="DishId"/> the raw name resolved to, plus the parsed <see cref="Price"/> and
/// <see cref="Weight"/>. These become live menu items on the persist side (subject to the admin
/// protection gate); unmapped lines never appear here — they go to <see cref="ParseQuarantineItem"/>.
/// </summary>
public sealed record NormalizedMenuItem(Guid DishId, Money Price, string? Weight);

/// <summary>
/// The normalizer's <b>mapped / unmapped split</b> (invariant #2): every raw dish in a menu is routed
/// to exactly one of two buckets — <see cref="MappedItems"/> (resolved to a canonical dish, persisted
/// as live menu items) or <see cref="Quarantined"/> (no canonical match, routed to the review queue
/// rather than dropped or failing the parse). A menu with some unmappable and some mappable dishes
/// therefore yields a non-empty result in <i>both</i> buckets, which is exactly the behaviour the
/// quarantine tests assert.
/// </summary>
public sealed record NormalizationResult(
    IReadOnlyList<NormalizedMenuItem> MappedItems,
    IReadOnlyList<ParseQuarantineItem> Quarantined);
