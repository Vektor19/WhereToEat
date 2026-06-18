using WhereToEat.Catalog.Application.Abstractions;
using WhereToEat.Catalog.Application.Contracts;

namespace WhereToEat.Catalog.Application.Queries;

/// <summary>
/// Lists every category (the upper taxonomy level — invariant #2) for the user to pick from. This
/// is a parameterless, deterministic list query — selection from a list, not NLP (invariant #1).
/// </summary>
public sealed class ListCategoriesQuery
{
    private readonly ICatalogReadPort _readPort;

    public ListCategoriesQuery(ICatalogReadPort readPort)
    {
        ArgumentNullException.ThrowIfNull(readPort);
        _readPort = readPort;
    }

    /// <summary>Returns all categories as flat DTOs, ordered by name (the read port orders them).</summary>
    public async Task<IReadOnlyList<CategoryDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _readPort.ListCategoriesAsync(cancellationToken).ConfigureAwait(false);
        return categories
            .Select(c => new CategoryDto(c.Id.Value, c.Name))
            .ToList();
    }
}
