using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Monetization.Domain;
using WhereToEat.Monetization.Domain.Abstractions;
using WhereToEat.Monetization.Domain.Identifiers;

namespace WhereToEat.Monetization.Infrastructure.Persistence;

/// <summary>
/// The Dapper-backed <see cref="IVerifiedStatusRepository"/>: a single keyed lookup for the per-venue
/// status and a <c>MERGE</c>-based idempotent upsert. Rows are rehydrated through the domain factory
/// (<see cref="VerifiedStatus.Create"/>) so the same invariants apply on read as on write. No EF Core —
/// Dapper only (EF is Admin.Infrastructure-only). The status it stores gates the Step 5 real-photo
/// permission; it never touches organic ranking (invariant #10) or the always-free contact links (§5.8).
/// </summary>
public sealed class DapperVerifiedStatusRepository : IVerifiedStatusRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperVerifiedStatusRepository(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<VerifiedStatus?> GetByVenueAsync(VenueRef venue, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<VerifiedStatusRow>(
            new CommandDefinition(
                MonetizationSql.SelectVerifiedStatusByVenue,
                new { VenueId = venue.Value },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var result = VerifiedStatus.Create(VenueRef.From(row.VenueId), (SubscriptionTier)row.Tier);
        if (result.IsFailure)
        {
            // A stored row that violates the domain invariants is a data-integrity bug, not
            // recoverable input — surface it loudly rather than returning a silently-wrong status.
            throw new InvalidOperationException(
                $"Stored Verified status for venue '{row.VenueId}' is invalid: {result.Error.Message}");
        }

        return result.Value;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(VerifiedStatus status, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(status);

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                MonetizationSql.UpsertVerifiedStatus,
                new
                {
                    VenueId = status.Venue.Value,
                    Tier = (int)status.Tier,
                },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
