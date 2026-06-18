using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereToEat.Admin.Infrastructure.Entities;

namespace WhereToEat.Admin.Infrastructure.Configurations;

/// <summary>
/// Database-first mapping of <see cref="MenuItemEntity"/> onto the EXACT <c>catalog.MenuItem</c>
/// table/columns the Step 3/4 SQL script created. The script's <c>RawSnapshot</c> JSON column is left
/// unmapped (parser-owned). EF owns no schema — it describes the existing shape so admin CRUD can edit
/// price/weight/dish, set provenance, and toggle the per-item <c>DoNotParse</c> flag (invariant #3).
/// </summary>
public sealed class MenuItemEntityConfiguration : IEntityTypeConfiguration<MenuItemEntity>
{
    public void Configure(EntityTypeBuilder<MenuItemEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MenuItem", "catalog");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("Id").ValueGeneratedNever();
        builder.Property(m => m.RestaurantId).HasColumnName("RestaurantId");
        builder.Property(m => m.DishId).HasColumnName("DishId");
        builder.Property(m => m.PriceAmount).HasColumnName("PriceAmount").HasColumnType("decimal(18,2)");
        builder.Property(m => m.PriceCurrency).HasColumnName("PriceCurrency");
        builder.Property(m => m.Weight).HasColumnName("Weight");
        builder.Property(m => m.Source).HasColumnName("Source");
        builder.Property(m => m.DoNotParse).HasColumnName("DoNotParse");
    }
}
