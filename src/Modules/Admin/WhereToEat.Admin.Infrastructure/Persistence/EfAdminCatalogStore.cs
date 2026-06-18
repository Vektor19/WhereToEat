using Microsoft.EntityFrameworkCore;
using WhereToEat.Admin.Application.Abstractions;
using WhereToEat.Admin.Infrastructure.Entities;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Admin.Infrastructure.Persistence;

/// <summary>
/// The EF Core implementation of <see cref="IAdminCatalogStore"/> over the existing SQL-script-owned
/// schema (database-first). All admin writes flow through here; EF change-tracking does the update
/// SQL. The store enforces the admin-CRUD guards (unknown row → NotFound, invalid price → Validation,
/// generic photo cannot be permission-gated) and stamps menu-item edits with the <b>Admin</b>
/// provenance so a later parse treats them as curated (invariant #3).
/// </summary>
public sealed class EfAdminCatalogStore : IAdminCatalogStore
{
    // catalog.MenuItem.Source value for admin-sourced provenance (matches the Catalog SourceKind
    // enum's Admin = 1; kept as a local constant so the Admin module never references Catalog.Domain).
    private const int AdminSource = 1;

    private readonly AdminDbContext _db;

    public EfAdminCatalogStore(AdminDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Result> EditMenuItemAsync(
        Guid menuItemId,
        Guid dishId,
        decimal priceAmount,
        string priceCurrency,
        string? weight,
        CancellationToken cancellationToken = default)
    {
        if (priceAmount < 0)
        {
            return Result.Failure(Error.Validation(
                "Admin.MenuItem.NegativePrice", "A menu item price cannot be negative."));
        }

        if (string.IsNullOrWhiteSpace(priceCurrency) || priceCurrency.Trim().Length != 3)
        {
            return Result.Failure(Error.Validation(
                "Admin.MenuItem.InvalidCurrency", "A menu item price needs a 3-letter ISO 4217 currency."));
        }

        var item = await _db.MenuItems
            .FirstOrDefaultAsync(m => m.Id == menuItemId, cancellationToken)
            .ConfigureAwait(false);
        if (item is null)
        {
            return Result.Failure(Error.NotFound(
                "Admin.MenuItem.NotFound", $"Menu item {menuItemId} was not found."));
        }

        var dishExists = await _db.Dishes
            .AnyAsync(d => d.Id == dishId, cancellationToken)
            .ConfigureAwait(false);
        if (!dishExists)
        {
            return Result.Failure(Error.NotFound(
                "Admin.Dish.NotFound", $"Target dish {dishId} was not found (re-categorisation needs an existing dish)."));
        }

        item.DishId = dishId;
        item.PriceAmount = priceAmount;
        item.PriceCurrency = priceCurrency.Trim().ToUpperInvariant();
        item.Weight = string.IsNullOrWhiteSpace(weight) ? null : weight.Trim();
        // Admin edit = source of truth; mark provenance so the parser treats it as curated (invariant #3).
        item.Source = AdminSource;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> SetMenuItemDoNotParseAsync(
        Guid menuItemId,
        bool doNotParse,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.MenuItems
            .FirstOrDefaultAsync(m => m.Id == menuItemId, cancellationToken)
            .ConfigureAwait(false);
        if (item is null)
        {
            return Result.Failure(Error.NotFound(
                "Admin.MenuItem.NotFound", $"Menu item {menuItemId} was not found."));
        }

        item.DoNotParse = doNotParse;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> SetRestaurantDoNotUpdateAsync(
        Guid restaurantId,
        bool doNotUpdate,
        CancellationToken cancellationToken = default)
    {
        var restaurantExists = await _db.Restaurants
            .AnyAsync(r => r.Id == restaurantId, cancellationToken)
            .ConfigureAwait(false);
        if (!restaurantExists)
        {
            return Result.Failure(Error.NotFound(
                "Admin.Restaurant.NotFound", $"Restaurant {restaurantId} was not found."));
        }

        var protection = await _db.RestaurantProtections
            .FirstOrDefaultAsync(p => p.RestaurantId == restaurantId, cancellationToken)
            .ConfigureAwait(false);

        if (protection is null)
        {
            // Upsert: no protection row yet — create one for this venue.
            _db.RestaurantProtections.Add(new RestaurantProtectionEntity
            {
                RestaurantId = restaurantId,
                DoNotUpdate = doNotUpdate,
            });
        }
        else
        {
            protection.DoNotUpdate = doNotUpdate;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> EditAddressAsync(
        Guid restaurantId,
        string addressLine,
        string? city,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(addressLine))
        {
            return Result.Failure(Error.Validation(
                "Admin.Address.LineRequired", "An address line cannot be blank."));
        }

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.Id == restaurantId, cancellationToken)
            .ConfigureAwait(false);
        if (restaurant is null)
        {
            return Result.Failure(Error.NotFound(
                "Admin.Restaurant.NotFound", $"Restaurant {restaurantId} was not found."));
        }

        restaurant.AddressLine = addressLine.Trim();
        restaurant.City = string.IsNullOrWhiteSpace(city) ? null : city.Trim();

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> SetRealPhotoPermissionAsync(
        Guid photoId,
        bool permissionGranted,
        CancellationToken cancellationToken = default)
    {
        var photo = await _db.Photos
            .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken)
            .ConfigureAwait(false);
        if (photo is null)
        {
            return Result.Failure(Error.NotFound(
                "Admin.Photo.NotFound", $"Photo {photoId} was not found."));
        }

        if (photo.IsGeneric)
        {
            // Our own generic content is never permission-gated (invariant #8); the DB CHECK enforces
            // this too, so reject it here with a clear message rather than hitting a constraint error.
            return Result.Failure(Error.Validation(
                "Admin.Photo.GenericNotGated",
                "A generic category photo is our own content and is never permission-gated."));
        }

        photo.PermissionGranted = permissionGranted;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
