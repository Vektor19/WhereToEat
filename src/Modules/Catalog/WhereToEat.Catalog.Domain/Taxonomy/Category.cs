using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Catalog.Domain.Taxonomy;

/// <summary>
/// The upper level of the two-level taxonomy (invariant #2): a <b>group</b> of dishes
/// (Перші страви, Фаст-фуд, Десерти…), never a concrete dish itself. A user can search by a
/// whole category or drill into a specific <see cref="Dish"/> within it. A category owns no
/// child collection here — dishes reference their category by <see cref="CategoryId"/> (each
/// aggregate stays small and is loaded independently), which is exactly the
/// <c>many Dishes — 1 Category</c> relationship from the design's ER model.
/// </summary>
public sealed class Category : AggregateRoot<CategoryId>
{
    private Category(CategoryId id, string name)
        : base(id)
    {
        Name = name;
    }

    /// <summary>The display name of the group (e.g. "Перші страви"). Always non-blank.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// Creates a category, rejecting a blank name. Returns a failure <see cref="Result"/>
    /// rather than throwing because a blank name is expected, recoverable input (e.g. from an
    /// admin form). The name is trimmed so trailing/leading whitespace never leaks into search.
    /// </summary>
    public static Result<Category> Create(string name)
        => Create(CategoryId.New(), name);

    /// <summary>
    /// Creates a category with an explicit id (used when rehydrating a persisted row or seeding
    /// a known taxonomy). Same blank-name guard as <see cref="Create(string)"/>.
    /// </summary>
    public static Result<Category> Create(CategoryId id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Category>(
                Error.Validation("Category.NameRequired", "A category name cannot be blank."));
        }

        return Result.Success(new Category(id, name.Trim()));
    }

    /// <summary>
    /// Renames the category (admin edit), rejecting a blank name. Admin edits are the source of
    /// truth over the parser (invariant #3), so this guard protects the curated name.
    /// </summary>
    public Result Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(
                Error.Validation("Category.NameRequired", "A category name cannot be blank."));
        }

        Name = name.Trim();
        return Result.Success();
    }
}
