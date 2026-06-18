using WhereToEat.Analytics.Domain.Anonymization;

namespace WhereToEat.Analytics.Infrastructure.Persistence;

/// <summary>
/// The JSON document shape stored in the analytics detail column, mirroring the retained
/// <see cref="EventDimensions"/>. Kept as a plain serializable record so the variable bag of
/// non-identifying dimensions rides in one column without a wide relational shape. Internal — a
/// persistence detail of the writer, not part of the module's public surface.
/// </summary>
internal sealed record EventDimensionsDocument(
    Guid? RestaurantId,
    IReadOnlyList<Guid> CategoryIds,
    IReadOnlyList<Guid> DishIds,
    string? SortMode,
    IReadOnlyList<string> Filters,
    int? Position)
{
    public static EventDimensionsDocument From(EventDimensions dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        return new EventDimensionsDocument(
            dimensions.RestaurantId,
            dimensions.CategoryIds,
            dimensions.DishIds,
            dimensions.SortMode,
            dimensions.Filters,
            dimensions.Position);
    }

    /// <summary>Rebuilds the domain dimensions value object from the stored document.</summary>
    public EventDimensions ToDomain()
        => EventDimensions.Create(RestaurantId, CategoryIds, DishIds, SortMode, Filters, Position);
}
