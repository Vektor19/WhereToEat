using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Contracts.Parsing;

namespace WhereToEat.Catalog.Infrastructure.Parsing;

/// <summary>
/// The production Catalog-side adapter for the Parsing module's <see cref="ICatalogDishResolver"/> seam
/// (Step 9 / Step 13): a <b>deterministic exact lookup</b> of a raw parsed dish name against
/// <c>catalog.Dish.CanonicalName</c> — no NLP (invariant #1). It returns the canonical dish + its
/// category id when matched, or <c>null</c> when no canonical dish matches (the caller then routes the
/// raw item to the quarantine queue — never dropped, never fatal — invariant #2).
/// <para>
/// It lives inside the Catalog module (over the shared connection factory), so the Parsing module
/// depends only on the <c>Contracts</c> port and never references Catalog internals (Step 2
/// module-isolation rule (d) stays green).
/// </para>
/// </summary>
public sealed class DapperCatalogDishResolver : ICatalogDishResolver
{
    private const string SelectByCanonicalName =
        "SELECT Id, CategoryId FROM catalog.Dish WHERE CanonicalName = @Name;";

    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperCatalogDishResolver(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<CanonicalDishDto?> ResolveAsync(
        string rawDishName,
        string? categoryHint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawDishName);

        using var connection = await _connectionFactory
            .CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<DishRow>(
                new CommandDefinition(
                    SelectByCanonicalName,
                    new { Name = rawDishName },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        // A null result is the documented "unmappable" signal that quarantines the raw item; the
        // category hint is never used to invent a match (deterministic resolution only). A reference
        // (class) row type is used so a miss is an unambiguous null — Dapper does not reliably map a
        // multi-column result into a Nullable<struct>, which would mask a real match as "unmappable".
        return row is null ? null : new CanonicalDishDto(row.Id, row.CategoryId);
    }

    private sealed record DishRow(Guid Id, Guid CategoryId);
}
