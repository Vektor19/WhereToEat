using WhereToEat.Catalog.Application.Abstractions;
using WhereToEat.Catalog.Application.Contracts;
using WhereToEat.Catalog.Domain.Identifiers;

namespace WhereToEat.Catalog.Application.Queries;

/// <summary>
/// Lists the dishes inside one category (the "drill into a category" path of §5.4). A deterministic
/// id-scoped list — no NLP (invariant #1). An unknown/childless category yields an empty list.
/// </summary>
public sealed class ListDishesByCategoryQuery
{
    private readonly ICatalogReadPort _readPort;

    public ListDishesByCategoryQuery(ICatalogReadPort readPort)
    {
        ArgumentNullException.ThrowIfNull(readPort);
        _readPort = readPort;
    }

    /// <summary>Returns the dishes of <paramref name="categoryId"/> as flat DTOs, ordered by name.</summary>
    public async Task<IReadOnlyList<DishDto>> ExecuteAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var dishes = await _readPort
            .ListDishesByCategoryAsync(CategoryId.From(categoryId), cancellationToken)
            .ConfigureAwait(false);

        return dishes
            .Select(d => new DishDto(d.Id.Value, d.CategoryId.Value, d.CanonicalName))
            .ToList();
    }
}
