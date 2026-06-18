using WhereToEat.Catalog.Application.Abstractions;
using WhereToEat.Catalog.Application.Contracts;

namespace WhereToEat.Catalog.Search.Application;

/// <summary>
/// Deterministic <b>prefix</b> lookup of categories for building the user's selection set
/// (invariant #1: selection from lists, never natural-language understanding). The query takes a
/// literal prefix and a result cap — there is no tokenisation, ranking, stemming, fuzzy matching,
/// or any NLP; the underlying repository runs an anchored <c>LIKE 'prefix%'</c>.
/// </summary>
public sealed class SearchCategoriesQuery
{
    /// <summary>The cap applied when a caller does not specify one — keeps the picklist short.</summary>
    public const int DefaultLimit = 20;

    /// <summary>The hard upper bound so a caller can never request an unbounded scan.</summary>
    public const int MaxLimit = 100;

    private readonly ICatalogReadPort _readPort;

    public SearchCategoriesQuery(ICatalogReadPort readPort)
    {
        ArgumentNullException.ThrowIfNull(readPort);
        _readPort = readPort;
    }

    /// <summary>
    /// Returns categories whose name starts with <paramref name="prefix"/> (case-insensitive),
    /// capped at <paramref name="limit"/>. A blank prefix yields an empty list (nothing to filter on).
    /// </summary>
    public async Task<IReadOnlyList<CategoryDto>> ExecuteAsync(
        string? prefix,
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return [];
        }

        var effectiveLimit = SearchLimits.Clamp(limit, DefaultLimit, MaxLimit);
        var categories = await _readPort
            .SearchCategoriesByPrefixAsync(prefix.Trim(), effectiveLimit, cancellationToken)
            .ConfigureAwait(false);

        return categories
            .Select(c => new CategoryDto(c.Id.Value, c.Name))
            .ToList();
    }
}
