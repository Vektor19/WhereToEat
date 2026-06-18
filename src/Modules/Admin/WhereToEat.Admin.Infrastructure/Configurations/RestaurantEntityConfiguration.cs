using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereToEat.Admin.Infrastructure.Entities;

namespace WhereToEat.Admin.Infrastructure.Configurations;

/// <summary>
/// Database-first mapping of <see cref="RestaurantEntity"/> onto the EXACT
/// <c>catalog.Restaurant</c> table/columns the Step 4 SQL script created. EF owns no schema here — it
/// only describes the existing shape so the admin CRUD can read/write it. The script's
/// <c>Location</c> (geography) and <c>RawSnapshot</c> (JSON) columns are intentionally NOT mapped
/// (they are geocoder/parser-owned, outside admin CRUD), and EF must not treat their absence as a
/// model divergence — the <see cref="EfMigrationsDisabledGuard"/> tolerates extra DB columns, never
/// missing ones.
/// </summary>
public sealed class RestaurantEntityConfiguration : IEntityTypeConfiguration<RestaurantEntity>
{
    public void Configure(EntityTypeBuilder<RestaurantEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Restaurant", "catalog");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("Id").ValueGeneratedNever();
        builder.Property(r => r.Name).HasColumnName("Name");
        builder.Property(r => r.AddressLine).HasColumnName("AddressLine");
        builder.Property(r => r.City).HasColumnName("AddressCity");
    }
}
