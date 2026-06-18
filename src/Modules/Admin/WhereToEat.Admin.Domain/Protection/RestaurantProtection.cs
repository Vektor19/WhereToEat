using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Admin.Domain.Protection;

/// <summary>
/// The restaurant-level <b>admin > parser</b> protection (invariant #3): when an operator has vetted a
/// venue by hand, the <see cref="DoNotUpdate"/> flag tells the parser to <b>skip the whole venue</b> so
/// its hand-curated data is never overwritten by a "draft" parse. This complements the per-MenuItem
/// <c>DoNotParse</c> flag from Step 3 (which shields a single item): together the parser persist gate
/// (Step 9) consults <see cref="ShouldParserSkip"/> at the restaurant level and <c>DoNotParse</c> at
/// the item level.
///
/// <para>
/// Keyed by an opaque <see cref="VenueRef"/> so the Admin domain stays independent of Catalog
/// (module-isolation rule). Persistence-free domain shape; the EF-backed admin CRUD that sets it
/// arrives in Step 10.
/// </para>
/// </summary>
public sealed class RestaurantProtection : AggregateRoot<VenueRef>
{
    private RestaurantProtection(VenueRef venue, bool doNotUpdate)
        : base(venue)
    {
        DoNotUpdate = doNotUpdate;
    }

    /// <summary>The venue these flags protect (the aggregate's identity).</summary>
    public VenueRef Venue => Id;

    /// <summary>
    /// When <c>true</c>, the parser must not create or overwrite this venue's data — the venue was
    /// vetted by hand and the admin is the source of truth (invariant #3).
    /// </summary>
    public bool DoNotUpdate { get; private set; }

    /// <summary>Creates an unprotected entry (the default — the parser may update the venue).</summary>
    public static Result<RestaurantProtection> Unprotected(VenueRef venue) => Create(venue, doNotUpdate: false);

    /// <summary>
    /// Creates a protection entry with an explicit flag (for rehydration/seeding), rejecting a default
    /// venue reference.
    /// </summary>
    public static Result<RestaurantProtection> Create(VenueRef venue, bool doNotUpdate)
    {
        if (venue == default)
        {
            return Result.Failure<RestaurantProtection>(
                Error.Validation("RestaurantProtection.VenueRequired", "A protection entry must reference a venue."));
        }

        return Result.Success(new RestaurantProtection(venue, doNotUpdate));
    }

    /// <summary>Marks the venue as hand-curated — the parser must skip it (invariant #3).</summary>
    public void ProtectFromParser() => DoNotUpdate = true;

    /// <summary>Clears the protection — the parser may update the venue again.</summary>
    public void AllowParserUpdates() => DoNotUpdate = false;

    /// <summary>
    /// Whether the parser persist gate (Step 9) must skip this venue entirely. Equal to
    /// <see cref="DoNotUpdate"/>; expressed as a method so the gate reads as an intent check.
    /// </summary>
    public bool ShouldParserSkip() => DoNotUpdate;
}
