using WhereToEat.SharedKernel.Primitives;

namespace WhereToEat.Analytics.Domain.Anonymization;

/// <summary>
/// The <b>retained</b> non-identifying dimensions of an analytics event (§8.1): the dish/category ids
/// the user worked with, the sort mode and filter selections, and the result <b>position</b>. These
/// are demand/behaviour signals with no link to a person, so they are kept as-is — the privacy line is
/// drawn at <see cref="Geohash"/>/<see cref="HashedActorId"/>/<see cref="HourBucket"/>, not here.
/// Equality is by value (the id sets compare element-wise, in order).
/// </summary>
public sealed class EventDimensions : ValueObject
{
    private EventDimensions(
        Guid? restaurantId,
        IReadOnlyList<Guid> categoryIds,
        IReadOnlyList<Guid> dishIds,
        string? sortMode,
        IReadOnlyList<string> filters,
        int? position)
    {
        RestaurantId = restaurantId;
        CategoryIds = categoryIds;
        DishIds = dishIds;
        SortMode = sortMode;
        Filters = filters;
        Position = position;
    }

    /// <summary>
    /// The restaurant whose card the event concerns (e.g. shown/opened), or null for events with no
    /// single restaurant (a search/filter selection). It identifies a <i>venue</i>, not a person, so it
    /// is retained — and it is what the per-restaurant rollup groups by.
    /// </summary>
    public Guid? RestaurantId { get; }

    /// <summary>The category ids involved (e.g. the selection set or a shown card's category).</summary>
    public IReadOnlyList<Guid> CategoryIds { get; }

    /// <summary>The dish ids involved.</summary>
    public IReadOnlyList<Guid> DishIds { get; }

    /// <summary>The sort mode key (e.g. "price", "best"), or null when not applicable.</summary>
    public string? SortMode { get; }

    /// <summary>The applied filter keys (e.g. "price", "rating"), in selection order.</summary>
    public IReadOnlyList<string> Filters { get; }

    /// <summary>The 1-based position of the card in a result list, or null when not applicable.</summary>
    public int? Position { get; }

    /// <summary>
    /// Builds the retained dimensions. Nulls are normalized to empty collections so a stored row has a
    /// stable shape; the values themselves are non-identifying and kept verbatim.
    /// </summary>
    public static EventDimensions Create(
        Guid? restaurantId = null,
        IEnumerable<Guid>? categoryIds = null,
        IEnumerable<Guid>? dishIds = null,
        string? sortMode = null,
        IEnumerable<string>? filters = null,
        int? position = null)
        => new(
            restaurantId == Guid.Empty ? null : restaurantId,
            categoryIds is null ? [] : [.. categoryIds],
            dishIds is null ? [] : [.. dishIds],
            string.IsNullOrWhiteSpace(sortMode) ? null : sortMode.Trim(),
            filters is null ? [] : [.. filters],
            position);

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RestaurantId;

        foreach (var id in CategoryIds)
        {
            yield return id;
        }

        // A separator so {[a],[]} and {[],[a]} are not accidentally equal across the two id lists.
        yield return "|dishes|";

        foreach (var id in DishIds)
        {
            yield return id;
        }

        yield return SortMode;

        foreach (var filter in Filters)
        {
            yield return filter;
        }

        yield return Position;
    }
}
