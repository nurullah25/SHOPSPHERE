using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Data.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Coupons_DiscountValue", "[DiscountValue] > 0");
            t.HasCheckConstraint("CK_Coupons_Percentage", "[DiscountType] <> 'Percentage' OR [DiscountValue] <= 100");
            t.HasCheckConstraint("CK_Coupons_TimesUsed", "[UsageLimit] IS NULL OR [TimesUsed] <= [UsageLimit]");
        });

        builder.Property(c => c.Code).HasMaxLength(40).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(300);

        builder.HasIndex(c => c.Code).IsUnique();
    }
}
