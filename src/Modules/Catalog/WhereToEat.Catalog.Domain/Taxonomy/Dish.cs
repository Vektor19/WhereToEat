using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Catalog.Domain.Taxonomy;

/// <summary>
/// The lower level of the two-level taxonomy (invariant #2): a <b>concrete position</b> inside
/// exactly one <see cref="Category"/> (Борщ, Піца Маргарита, Картопля фрі…). This carries the
/// <b>canonical normalized name</b> the parser maps raw menu strings onto, so search and the
/// dish-at-restaurant <see cref="Restaurants.MenuItem"/> all converge on one spelling.
///
/// The "belongs to exactly one Category" half of the invariant is structural: a <see cref="Dish"/>
/// cannot be constructed without a <see cref="CategoryId"/>, and that id is immutable once set —
/// re-categorising is an explicit <see cref="MoveToCategory"/> operation, not an open setter.
/// </summary>
public sealed class Dish : AggregateRoot<DishId>
{
    private Dish(DishId id, CategoryId categoryId, string canonicalName)
        : base(id)
    {
        CategoryId = categoryId;
        CanonicalName = canonicalName;
    }

    /// <summary>The category this dish belongs to — never empty (invariant #2). Mutated only via
    /// <see cref="MoveToCategory"/> so the two-level link is always explicit.</summary>
    public CategoryId CategoryId { get; private set; }

    /// <summary>The canonical/normalized dish name raw parsed names are mapped onto. Non-blank.</summary>
    public string CanonicalName { get; private set; }

    /// <summary>
    /// Creates a dish under a category, enforcing the two-level taxonomy: the
    /// <paramref name="categoryId"/> must be a real (non-default) <see cref="CategoryId"/> and the
    /// name must be non-blank. A default <see cref="CategoryId"/> is treated as "no category" and
    /// rejected, so a free-floating dish (which would break invariant #2) cannot exist.
    /// </summary>
    public static Result<Dish> Create(CategoryId categoryId, string canonicalName)
        => Create(DishId.New(), categoryId, canonicalName);

    /// <summary>
    /// Creates a dish with an explicit id (rehydration/seeding). Same category and name guards as
    /// <see cref="Create(CategoryId, string)"/>.
    /// </summary>
    public static Result<Dish> Create(DishId id, CategoryId categoryId, string canonicalName)
    {
        if (categoryId == default)
        {
            return Result.Failure<Dish>(
                Error.Validation(
                    "Dish.CategoryRequired",
                    "A dish must belong to a category (two-level taxonomy)."));
        }

        if (string.IsNullOrWhiteSpace(canonicalName))
        {
            return Result.Failure<Dish>(
                Error.Validation("Dish.NameRequired", "A dish name cannot be blank."));
        }

        return Result.Success(new Dish(id, categoryId, canonicalName.Trim()));
    }

    /// <summary>
    /// Renames the dish's canonical name (admin curation), rejecting a blank name. Because raw
    /// parsed names normalise onto this value, the curated spelling is protected (invariant #3).
    /// </summary>
    public Result Rename(string canonicalName)
    {
        if (string.IsNullOrWhiteSpace(canonicalName))
        {
            return Result.Failure(
                Error.Validation("Dish.NameRequired", "A dish name cannot be blank."));
        }

        CanonicalName = canonicalName.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Re-categorises the dish, keeping the "exactly one non-empty category" invariant: a default
    /// <see cref="CategoryId"/> is rejected so a dish can never be moved into "no category".
    /// </summary>
    public Result MoveToCategory(CategoryId categoryId)
    {
        if (categoryId == default)
        {
            return Result.Failure(
                Error.Validation(
                    "Dish.CategoryRequired",
                    "A dish must belong to a category (two-level taxonomy)."));
        }

        CategoryId = categoryId;
        return Result.Success();
    }
}
