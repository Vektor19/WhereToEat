using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereToEat.Admin.Infrastructure.Entities;

namespace WhereToEat.Admin.Infrastructure.Configurations;

/// <summary>
/// Database-first mapping of <see cref="DishEntity"/> onto the EXACT <c>catalog.Dish</c> table/columns
/// the Step 4 SQL script created. Read-only for admin CRUD (validating a re-categorisation's target
/// dish exists). EF owns no schema — it only describes the existing shape.
/// </summary>
public sealed class DishEntityConfiguration : IEntityTypeConfiguration<DishEntity>
{
    public void Configure(EntityTypeBuilder<DishEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Dish", "catalog");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("Id").ValueGeneratedNever();
        builder.Property(d => d.CategoryId).HasColumnName("CategoryId");
        builder.Property(d => d.CanonicalName).HasColumnName("CanonicalName");
    }
}
