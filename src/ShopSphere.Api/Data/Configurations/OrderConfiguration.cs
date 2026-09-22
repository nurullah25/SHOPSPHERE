using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Orders_Amounts", "[Subtotal] >= 0 AND [DiscountAmount] >= 0 AND [ShippingCost] >= 0 AND [Total] >= 0");
        });

        builder.Property(o => o.OrderNumber)
            .HasMaxLength(20)
            .HasDefaultValueSql("CONCAT('SS-', NEXT VALUE FOR [OrderNumbers])");

        builder.Property(o => o.CouponCode).HasMaxLength(40);
        builder.Property(o => o.Notes).HasMaxLength(500);
        builder.Property(o => o.CancellationReason).HasMaxLength(300);
        builder.Property(o => o.RowVersion).IsRowVersion();

        builder.OwnsOne(o => o.ShippingAddress, address =>
        {
            address.Property(a => a.FullName).HasColumnName("ShippingFullName").HasMaxLength(200).IsRequired();
            address.Property(a => a.Line1).HasColumnName("ShippingLine1").HasMaxLength(200).IsRequired();
            address.Property(a => a.Line2).HasColumnName("ShippingLine2").HasMaxLength(200);
            address.Property(a => a.City).HasColumnName("ShippingCity").HasMaxLength(100).IsRequired();
            address.Property(a => a.State).HasColumnName("ShippingState").HasMaxLength(100);
            address.Property(a => a.PostalCode).HasColumnName("ShippingPostalCode").HasMaxLength(20).IsRequired();
            address.Property(a => a.Country).HasColumnName("ShippingCountry").HasMaxLength(100).IsRequired();
            address.Property(a => a.PhoneNumber).HasColumnName("ShippingPhoneNumber").HasMaxLength(30);
        });
        builder.Navigation(o => o.ShippingAddress).IsRequired();

        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => new { o.UserId, o.PlacedAt });
        builder.HasIndex(o => new { o.Status, o.PlacedAt });
        builder.HasIndex(o => o.PlacedAt);

        builder.HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Coupon)
            .WithMany()
            .HasForeignKey(o => o.CouponId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
