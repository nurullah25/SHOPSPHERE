using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Coupons;

public record CouponEvaluation(Coupon Coupon, decimal Discount);

public class CouponService
{
    private readonly AppDbContext _db;

    public CouponService(AppDbContext db)
    {
        _db = db;
    }

    // Every rule failure is a 400 with a message the customer can act on.
    // An invalid coupon never silently reduces the price.
    public async Task<CouponEvaluation> EvaluateAsync(string code, decimal subtotal, int userId)
    {
        var normalized = Normalize(code);
        var coupon = await _db.Coupons.SingleOrDefaultAsync(c => c.Code == normalized)
            ?? throw Invalid("This coupon code doesn't exist.");

        var now = DateTime.UtcNow;

        if (!coupon.IsActive)
            throw Invalid("This coupon is no longer active.");

        if (coupon.StartsAt.HasValue && coupon.StartsAt > now)
            throw Invalid("This coupon is not valid yet.");

        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt <= now)
            throw Invalid("This coupon has expired.");

        if (coupon.UsageLimit.HasValue && coupon.TimesUsed >= coupon.UsageLimit)
            throw Invalid("This coupon has reached its usage limit.");

        if (coupon.MinOrderAmount.HasValue && subtotal < coupon.MinOrderAmount)
            throw Invalid($"This coupon requires a minimum order of {coupon.MinOrderAmount:C}.");

        if (coupon.UsageLimitPerCustomer.HasValue)
        {
            var timesUsedByCustomer = await _db.CouponRedemptions
                .CountAsync(r => r.CouponId == coupon.Id && r.UserId == userId);

            if (timesUsedByCustomer >= coupon.UsageLimitPerCustomer)
                throw Invalid("You have already used this coupon.");
        }

        return new CouponEvaluation(coupon, CalculateDiscount(coupon, subtotal));
    }

    public static decimal CalculateDiscount(Coupon coupon, decimal subtotal)
    {
        var discount = coupon.DiscountType == DiscountType.Percentage
            ? Math.Round(subtotal * coupon.DiscountValue / 100m, 2, MidpointRounding.AwayFromZero)
            : coupon.DiscountValue;

        if (coupon.MaxDiscountAmount.HasValue)
            discount = Math.Min(discount, coupon.MaxDiscountAmount.Value);

        // A discount can never exceed the order itself
        return Math.Min(discount, subtotal);
    }

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    private static BusinessRuleException Invalid(string message) =>
        new("Invalid coupon", message, StatusCodes.Status400BadRequest);
}
