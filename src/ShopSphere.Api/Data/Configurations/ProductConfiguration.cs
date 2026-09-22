using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Products_Price", "[Price] >= 0");
            t.HasCheckConstraint("CK_Products_DiscountPrice", "[DiscountPrice] IS NULL OR ([DiscountPrice] >= 0 AND [DiscountPrice] < [Price])");
            t.HasCheckConstraint("CK_Products_StockQuantity", "[StockQuantity] >= 0");
        });

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(220).IsRequired();
        builder.Property(p => p.Sku).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(4000).IsRequired();

        builder.Property(p => p.EffectivePrice)
            .HasComputedColumnSql("COALESCE([DiscountPrice], [Price])", stored: true);

        builder.Property(p => p.AverageRating).HasPrecision(3, 2);
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasIndex(p => p.Sku).IsUnique();
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => new { p.CategoryId, p.IsActive });
        builder.HasIndex(p => p.EffectivePrice);
        builder.HasIndex(p => new { p.IsActive, p.StockQuantity });

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
