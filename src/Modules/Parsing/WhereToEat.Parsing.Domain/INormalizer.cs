namespace WhereToEat.Parsing.Domain;

/// <summary>
/// Maps the raw dish lines of a <see cref="ParsedMenu"/> onto the canonical Category → Dish taxonomy
/// (invariant #2), unifying units/prices, and returns the <see cref="NormalizationResult"/> split:
/// mappable lines become <see cref="NormalizedMenuItem"/>s, unmappable lines become
/// <see cref="ParseQuarantineItem"/>s (quarantined, <b>not dropped, not fatal</b>). The concrete
/// normalizer resolves canonical dishes through the <c>Contracts</c> <c>ICatalogDishResolver</c> seam,
/// so it never references Catalog internals.
/// </summary>
public interface INormalizer
{
    /// <summary>
    /// Normalizes <paramref name="menu"/>, routing each raw dish to the mapped or quarantined bucket.
    /// The whole operation never throws on an unmappable name and never fails the parse — that is the
    /// quarantine guarantee.
    /// </summary>
    Task<NormalizationResult> NormalizeAsync(ParsedMenu menu, CancellationToken cancellationToken = default);
}
