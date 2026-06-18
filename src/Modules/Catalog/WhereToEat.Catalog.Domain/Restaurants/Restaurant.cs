using WhereToEat.Catalog.Domain.Events;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Catalog.Domain.Restaurants;

/// <summary>
/// The <b>aggregate root</b> of the catalog model: a restaurant owning its <see cref="Address"/>,
/// optional <see cref="Coordinates"/> (OSM-sourced — invariant #7), its <see cref="ContactLink"/>s
/// (always shown for free, never monetized — invariant #10), and its <see cref="MenuItem"/>s (the
/// dish-at-restaurant joins). It is the consistency boundary that enforces the cross-child
/// invariants the design names:
/// <list type="bullet">
///   <item>at most <b>one menu item per dish</b> (no duplicate dish on a restaurant);</item>
///   <item>changing the <see cref="Address"/> raises an <see cref="AddressChanged"/> domain event
///   so the Geo module re-geocodes (the re-geocode wiring lives in Step 8).</item>
/// </list>
/// Children are created/mutated only through this root so those invariants always hold.
/// </summary>
public sealed class Restaurant : AggregateRoot<RestaurantId>
{
    private readonly List<MenuItem> _menuItems = [];
    private readonly List<ContactLink> _contactLinks = [];

    private Restaurant(RestaurantId id, string name, Address address)
        : base(id)
    {
        Name = name;
        Address = address;
    }

    /// <summary>The restaurant's display name. Always non-blank.</summary>
    public string Name { get; private set; }

    /// <summary>The (admin-editable) address. Changing it raises <see cref="AddressChanged"/>.</summary>
    public Address Address { get; private set; }

    /// <summary>The OSM-sourced coordinates, when geocoded; otherwise <c>null</c>.</summary>
    public Coordinates? Coordinates { get; private set; }

    /// <summary>The menu items (dish-at-restaurant joins); at most one per dish.</summary>
    public IReadOnlyCollection<MenuItem> MenuItems => _menuItems.AsReadOnly();

    /// <summary>The always-shown, never-monetized website/social links (invariant #10).</summary>
    public IReadOnlyCollection<ContactLink> ContactLinks => _contactLinks.AsReadOnly();

    /// <summary>Creates a restaurant with a fresh id; see <see cref="Create(RestaurantId, string, Address)"/>.</summary>
    public static Result<Restaurant> Create(string name, Address address)
        => Create(RestaurantId.New(), name, address);

    /// <summary>
    /// Creates a restaurant (with an explicit id for rehydration/seeding), rejecting a blank name
    /// or a null address. Coordinates start absent — they are set once geocoded (Step 8).
    /// </summary>
    public static Result<Restaurant> Create(RestaurantId id, string name, Address address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Restaurant>(
                Error.Validation("Restaurant.NameRequired", "A restaurant name cannot be blank."));
        }

        if (address is null)
        {
            return Result.Failure<Restaurant>(
                Error.Validation("Restaurant.AddressRequired", "A restaurant must have an address."));
        }

        return Result.Success(new Restaurant(id, name.Trim(), address));
    }

    /// <summary>Renames the restaurant (admin edit), rejecting a blank name.</summary>
    public Result Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(
                Error.Validation("Restaurant.NameRequired", "A restaurant name cannot be blank."));
        }

        Name = name.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Changes the address and raises an <see cref="AddressChanged"/> event so the geocoder refreshes
    /// the coordinates (invariant #7). A null address is rejected; setting the same address is a no-op
    /// that raises no event (nothing changed, nothing to re-geocode).
    /// </summary>
    public Result ChangeAddress(Address address)
    {
        if (address is null)
        {
            return Result.Failure(
                Error.Validation("Restaurant.AddressRequired", "A restaurant must have an address."));
        }

        if (Address == address)
        {
            return Result.Success();
        }

        Address = address;
        RaiseDomainEvent(AddressChanged.Now(Id, address));
        return Result.Success();
    }

    /// <summary>
    /// Sets the OSM-sourced coordinates (called by the geocode flow in Step 8). A null value clears
    /// them. Because <see cref="Restaurants.Coordinates"/> has no "from Google coords" factory, this
    /// can never store raw Google lat/lng (invariant #7).
    /// </summary>
    public void SetCoordinates(Coordinates? coordinates) => Coordinates = coordinates;

    /// <summary>Adds a contact link, ignoring an exact duplicate (same kind + URL).</summary>
    public Result AddContactLink(ContactLink link)
    {
        if (link is null)
        {
            return Result.Failure(
                Error.Validation("Restaurant.ContactLinkRequired", "A contact link is required."));
        }

        if (!_contactLinks.Contains(link))
        {
            _contactLinks.Add(link);
        }

        return Result.Success();
    }

    /// <summary>
    /// Adds a menu item for a dish, enforcing the <b>one menu item per dish</b> invariant: a second
    /// item for a dish already on the menu is rejected with a conflict. Price non-negativity comes
    /// from <see cref="Money"/>. Raises <see cref="MenuItemUpserted"/> on success.
    /// </summary>
    public Result<MenuItem> AddMenuItem(
        DishId dishId,
        Money price,
        string? weight = null,
        SourceKind source = SourceKind.Parsed,
        bool doNotParse = false)
        => AddMenuItem(MenuItemId.New(), dishId, price, weight, source, doNotParse);

    /// <summary>
    /// Adds a menu item with an explicit id (rehydration/seeding); same one-per-dish guard as
    /// <see cref="AddMenuItem(DishId, Money, string?, SourceKind, bool)"/>.
    /// </summary>
    public Result<MenuItem> AddMenuItem(
        MenuItemId id,
        DishId dishId,
        Money price,
        string? weight = null,
        SourceKind source = SourceKind.Parsed,
        bool doNotParse = false)
    {
        if (_menuItems.Any(m => m.DishId == dishId))
        {
            return Result.Failure<MenuItem>(
                Error.Conflict(
                    "Restaurant.DuplicateDish",
                    "A restaurant cannot have two menu items for the same dish."));
        }

        var itemResult = MenuItem.Create(id, dishId, price, weight, source, doNotParse);
        if (itemResult.IsFailure)
        {
            return Result.Failure<MenuItem>(itemResult.Error);
        }

        var item = itemResult.Value;
        _menuItems.Add(item);
        RaiseDomainEvent(MenuItemUpserted.Now(Id, item.Id, dishId));
        return Result.Success(item);
    }
}
