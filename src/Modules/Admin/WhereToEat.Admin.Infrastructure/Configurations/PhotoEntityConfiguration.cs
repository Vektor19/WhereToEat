using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereToEat.Admin.Infrastructure.Entities;

namespace WhereToEat.Admin.Infrastructure.Configurations;

/// <summary>
/// Database-first mapping of <see cref="PhotoEntity"/> onto the EXACT <c>catalog.Photo</c>
/// table/columns the Step 5 SQL script created. Admin CRUD toggles the real-photo permission gate
/// (invariant #8). EF owns no schema — it only describes the existing shape.
/// </summary>
public sealed class PhotoEntityConfiguration : IEntityTypeConfiguration<PhotoEntity>
{
    public void Configure(EntityTypeBuilder<PhotoEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Photo", "catalog");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("Id").ValueGeneratedNever();
        builder.Property(p => p.DishId).HasColumnName("DishId");
        builder.Property(p => p.IsGeneric).HasColumnName("IsGeneric");
        builder.Property(p => p.PermissionGranted).HasColumnName("PermissionGranted");
        builder.Property(p => p.Url).HasColumnName("Url");
    }
}
