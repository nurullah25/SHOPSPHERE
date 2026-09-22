namespace ShopSphere.Api.Entities;

public class CouponRedemption
{
    public int Id { get; set; }

    public int CouponId { get; set; }
    public Coupon Coupon { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
