using Microsoft.EntityFrameworkCore;
using WhereToEat.Admin.Infrastructure.Configurations;
using WhereToEat.Admin.Infrastructure.Entities;

namespace WhereToEat.Admin.Infrastructure;

/// <summary>
/// The Admin module's EF Core context — the ONLY EF context in the solution (the Step 2
/// EF-containment fitness rule confines EF to <c>Admin.Infrastructure</c>). It maps DATABASE-FIRST
/// onto the existing SQL-script-owned schema (Steps 4–5): every entity config in
/// <c>Configurations/*.cs</c> points at the EXACT table/column names the scripts created, and EF
/// NEVER owns or alters the schema.
///
/// <para>
/// <b>EF migrations are disabled</b>: there is no <c>Migrations</c> folder, the context never calls
/// <c>EnsureCreated</c>/<c>Migrate</c>, and the <see cref="EfMigrationsDisabledGuard"/> fails fast at
/// startup if EF's model diverges from the script schema in a way that would require a schema change.
/// The schema-of-record is the DbUp SQL scripts, never EF.
/// </para>
/// </summary>
public sealed class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options)
        : base(options)
    {
    }

    /// <summary>Restaurants (the <c>catalog.Restaurant</c> table) — name/address admin edits.</summary>
    public DbSet<RestaurantEntity> Restaurants => Set<RestaurantEntity>();

    /// <summary>Menu items (the <c>catalog.MenuItem</c> table) — price/weight/dish + DoNotParse edits.</summary>
    public DbSet<MenuItemEntity> MenuItems => Set<MenuItemEntity>();

    /// <summary>Dishes (the <c>catalog.Dish</c> table) — read-only, to validate re-categorisation.</summary>
    public DbSet<DishEntity> Dishes => Set<DishEntity>();

    /// <summary>Photos (the <c>catalog.Photo</c> table) — real-photo permission gate (invariant #8).</summary>
    public DbSet<PhotoEntity> Photos => Set<PhotoEntity>();

    /// <summary>Protection flags (the <c>admin.RestaurantProtection</c> table) — DoNotUpdate.</summary>
    public DbSet<RestaurantProtectionEntity> RestaurantProtections => Set<RestaurantProtectionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new RestaurantEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MenuItemEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DishEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PhotoEntityConfiguration());
        modelBuilder.ApplyConfiguration(new RestaurantProtectionEntityConfiguration());
    }
}
