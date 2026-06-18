namespace WhereToEat.Contracts.Parsing;

/// <summary>
/// The cross-module seam the <b>Parsing module</b> uses to map a <b>raw parsed dish name</b> onto the
/// canonical Category → Dish taxonomy (invariant #2) — <b>without</b> referencing Catalog internals.
/// Parsing depends only on this <c>Contracts</c> port; the Catalog module supplies the adapter (over
/// its own read model), so the catalog-domain coupling stays inside the Catalog module and the Step 2
/// module-isolation fitness rule stays green (modules talk only through <c>WhereToEat.Contracts</c>).
///
/// Resolution is <b>deterministic</b> (a normalized exact lookup, no NLP — invariant #1): a raw name
/// either matches a known canonical dish or it does not. A <c>null</c> result is the signal that the
/// raw item is <b>unmappable</b> and must be routed to the quarantine / review queue — it is never
/// dropped and never fails the parse.
/// </summary>
public interface ICatalogDishResolver
{
    /// <summary>
    /// Resolves <paramref name="rawDishName"/> to its canonical <see cref="CanonicalDishDto"/>, or
    /// <c>null</c> when no canonical dish matches (the caller then quarantines the raw item). The
    /// optional <paramref name="categoryHint"/> is a parser-supplied grouping label that an adapter
    /// may use to disambiguate, but a match is never invented from the hint alone.
    /// </summary>
    Task<CanonicalDishDto?> ResolveAsync(
        string rawDishName,
        string? categoryHint = null,
        CancellationToken cancellationToken = default);
}
