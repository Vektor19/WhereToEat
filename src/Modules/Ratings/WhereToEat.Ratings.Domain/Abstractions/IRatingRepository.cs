using WhereToEat.Ratings.Domain.Identifiers;

namespace WhereToEat.Ratings.Domain.Abstractions;

/// <summary>
/// The Ratings module's persistence port for the raw per-user <see cref="Rating"/> fact — the
/// <b>write side</b> that backs the submit-rating use-case (a user scoring a venue, invariant #6).
/// It sits alongside <see cref="IRatingAggregateRepository"/> (the hot read/rollup path) without
/// replacing it: this port owns the per-user fact, the aggregate port owns the materialized rollup the
/// recommendation engine ranks on. A Domain-shaped contract with no SQL leaks; the Dapper adapter in
/// <c>WhereToEat.Ratings.Infrastructure</c> implements it over <c>ratings.Rating</c> (one row per
/// user+restaurant, enforced by <c>UQ_Rating_Restaurant_User</c> — revising updates the row, never
/// duplicates).
/// </summary>
public interface IRatingRepository
{
    /// <summary>
    /// Reads the existing rating a <paramref name="user"/> left for <paramref name="restaurant"/>, or
    /// <c>null</c> if they have not rated it yet. The submit use-case uses this to decide between
    /// revising the existing fact and creating a new one (the unique constraint guarantees at most one).
    /// </summary>
    Task<Rating?> GetByRestaurantAndUserAsync(
        RestaurantRef restaurant,
        UserRef user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a brand-new <see cref="Rating"/> fact (the user had none for this restaurant). The
    /// caller has already validated the score through the domain factory; the unique
    /// (Restaurant, User) constraint backs the one-rating-per-user-per-restaurant invariant.
    /// </summary>
    Task AddAsync(Rating rating, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a revision of an existing <see cref="Rating"/> (same row, identified by its
    /// <see cref="RatingId"/>): overwrites the score and the given-at timestamp. Never inserts a second
    /// row for the same user+restaurant.
    /// </summary>
    Task UpdateAsync(Rating rating, CancellationToken cancellationToken = default);
}
