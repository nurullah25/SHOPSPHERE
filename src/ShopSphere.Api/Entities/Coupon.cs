namespace ShopSphere.Api.Entities;

public class Coupon : AuditableEntity
{
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }

    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }

    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public int? UsageLimit { get; set; }
    public int? UsageLimitPerCustomer { get; set; }
    public int TimesUsed { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<CouponRedemption> Redemptions { get; set; } = new List<CouponRedemption>();
}
