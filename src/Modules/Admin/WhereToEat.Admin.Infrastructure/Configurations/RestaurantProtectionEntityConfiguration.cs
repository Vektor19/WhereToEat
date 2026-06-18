using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereToEat.Admin.Infrastructure.Entities;

namespace WhereToEat.Admin.Infrastructure.Configurations;

/// <summary>
/// Database-first mapping of <see cref="RestaurantProtectionEntity"/> onto the EXACT
/// <c>admin.RestaurantProtection</c> table/columns the Step 5 SQL script created — keyed by the venue
/// Guid (invariant #3 venue-level protection). EF owns no schema — it only describes the existing
/// shape so admin CRUD can upsert the <c>DoNotUpdate</c> flag.
/// </summary>
public sealed class RestaurantProtectionEntityConfiguration : IEntityTypeConfiguration<RestaurantProtectionEntity>
{
    public void Configure(EntityTypeBuilder<RestaurantProtectionEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("RestaurantProtection", "admin");
        builder.HasKey(p => p.RestaurantId);

        builder.Property(p => p.RestaurantId).HasColumnName("RestaurantId").ValueGeneratedNever();
        builder.Property(p => p.DoNotUpdate).HasColumnName("DoNotUpdate");
    }
}
