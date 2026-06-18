using WhereToEat.Catalog.Application.Abstractions;
using WhereToEat.Catalog.Application.Contracts;

namespace WhereToEat.Catalog.Search.Application;

/// <summary>
/// Deterministic <b>prefix</b> lookup of dishes so a user can add a concrete position (e.g. "Борщ",
/// "Піца") to their selection (invariant #1: list/prefix selection, never NLP). As with category
/// search there is no free-text understanding — the repository runs an anchored <c>LIKE 'prefix%'</c>.
/// </summary>
public sealed class SearchDishesQuery
{
    /// <summary>The cap applied when a caller does not specify one.</summary>
    public const int DefaultLimit = 20;

    /// <summary>The hard upper bound so a caller can never request an unbounded scan.</summary>
    public const int MaxLimit = 100;

    private readonly ICatalogReadPort _readPort;

    public SearchDishesQuery(ICatalogReadPort readPort)
    {
        ArgumentNullException.ThrowIfNull(readPort);
        _readPort = readPort;
    }

    /// <summary>
    /// Returns dishes whose canonical name starts with <paramref name="prefix"/> (case-insensitive),
    /// capped at <paramref name="limit"/>. A blank prefix yields an empty list.
    /// </summary>
    public async Task<IReadOnlyList<DishDto>> ExecuteAsync(
        string? prefix,
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return [];
        }

        var effectiveLimit = SearchLimits.Clamp(limit, DefaultLimit, MaxLimit);
        var dishes = await _readPort
            .SearchDishesByPrefixAsync(prefix.Trim(), effectiveLimit, cancellationToken)
            .ConfigureAwait(false);

        return dishes
            .Select(d => new DishDto(d.Id.Value, d.CategoryId.Value, d.CanonicalName))
            .ToList();
    }
}
