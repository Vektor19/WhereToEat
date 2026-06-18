using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Taxonomy;

namespace WhereToEat.Catalog.Application.Abstractions;

/// <summary>
/// The catalog <b>query-side</b> read port: the list/lookup reads that back the public read and
/// deterministic-search use-cases (taxonomy lists + anchored prefix filters — invariant #1). It is
/// deliberately separate from the write-side <c>ICatalogRepository</c> (which loads/saves whole
/// aggregates for the parser/admin paths) so the read and write concerns do not share one fat port
/// — the read/write split the later modules follow. The same Dapper adapter implements both ports.
/// <para>
/// Reads return domain <see cref="Category"/>/<see cref="Dish"/> objects (not row/DTO shapes); the
/// application handlers project them to response DTOs. There is no tokenisation, ranking, stemming,
/// or fuzzy matching here — only ordered list and anchored <c>LIKE 'prefix%'</c> reads.
/// </para>
/// </summary>
public interface ICatalogReadPort
{
    /// <summary>
    /// Lists every category, ordered by name. Backs the deterministic taxonomy list the user picks
    /// from (invariant #1 — selection from lists, no NLP).
    /// </summary>
    Task<IReadOnlyList<Category>> ListCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the dishes that belong to <paramref name="categoryId"/>, ordered by canonical name
    /// (the "list dishes within a category" path of §5.4). Returns an empty list for an unknown or
    /// childless category.
    /// </summary>
    Task<IReadOnlyList<Dish>> ListDishesByCategoryAsync(CategoryId categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns categories whose name <b>starts with</b> <paramref name="prefix"/> (case-insensitive),
    /// ordered by name and capped at <paramref name="limit"/>. This is a deterministic prefix filter
    /// for building the user's selection set — <b>not</b> free-text/NLP search (invariant #1).
    /// </summary>
    Task<IReadOnlyList<Category>> SearchCategoriesByPrefixAsync(string prefix, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns dishes whose canonical name <b>starts with</b> <paramref name="prefix"/>
    /// (case-insensitive), ordered by name and capped at <paramref name="limit"/>. A deterministic
    /// prefix filter to add a concrete dish to the selection — <b>not</b> NLP (invariant #1).
    /// </summary>
    Task<IReadOnlyList<Dish>> SearchDishesByPrefixAsync(string prefix, int limit, CancellationToken cancellationToken = default);
}
