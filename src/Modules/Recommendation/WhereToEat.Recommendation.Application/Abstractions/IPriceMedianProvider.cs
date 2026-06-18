using WhereToEat.Contracts.Recommendation;

namespace WhereToEat.Recommendation.Application.Abstractions;

/// <summary>
/// The port <c>f_price</c> reads the market median through (CLAUDE.md §6). It returns the
/// <b>precomputed</b> per-area/category median basket amount for a selection (with a city-wide
/// fallback below the area sample threshold N), so the hot recommendation path only reads a ready
/// value — medians are <b>never</b> computed per request. The Step 7 implementation reads the median
/// <b>table directly from the DB (DB-only — no Redis)</b>; Step 13 later adds Redis transparently as
/// a caching decorator <b>over this same port</b>, so this contract and its callers never change.
/// </summary>
public interface IPriceMedianProvider
{
    /// <summary>
    /// Returns the precomputed median basket amount for <paramref name="items"/> near
    /// <paramref name="userGeo"/>, or <c>null</c> when no median is available (then <c>f_price</c>
    /// falls back to its neutral value). Reads a stored value only; performs no per-request
    /// aggregation.
    /// </summary>
    Task<decimal?> GetMedianBasketAmountAsync(
        IReadOnlyList<SelectedItem> items,
        UserGeo? userGeo,
        CancellationToken cancellationToken = default);
}
