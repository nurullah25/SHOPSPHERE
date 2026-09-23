using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Coupons;

namespace ShopSphere.Api.Features.Admin.Coupons;

public class AdminCouponDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? UsageLimit { get; set; }
    public int? UsageLimitPerCustomer { get; set; }
    public int TimesUsed { get; set; }
    public bool IsActive { get; set; }
}

public class CouponRequest
{
    [Required, MaxLength(40), RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Code may only contain letters, numbers and dashes.")]
    public string Code { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    [Required]
    public DiscountType DiscountType { get; set; }

    [Range(0.01, 100000)]
    public decimal DiscountValue { get; set; }

    [Range(0, 100000)]
    public decimal? MinOrderAmount { get; set; }

    [Range(0.01, 100000)]
    public decimal? MaxDiscountAmount { get; set; }

    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    [Range(1, 1000000)]
    public int? UsageLimit { get; set; }

    [Range(1, 1000)]
    public int? UsageLimitPerCustomer { get; set; }

    public bool IsActive { get; set; } = true;
}

[ApiController]
[Route("api/admin/coupons")]
[Authorize(Policy = Policies.Admin)]
public class AdminCouponsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminCouponsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminCouponDto>>> GetCoupons()
    {
        var coupons = await _db.Coupons
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Ok(coupons.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<AdminCouponDto>> Create(CouponRequest request)
    {
        Validate(request);

        var code = CouponService.Normalize(request.Code);
        if (await _db.Coupons.AnyAsync(c => c.Code == code))
            throw new BusinessRuleException("Duplicate code", $"A coupon with code {code} already exists.");

        var coupon = new Coupon { Code = code };
        Apply(coupon, request);

        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, ToDto(coupon));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminCouponDto>> Update(int id, CouponRequest request)
    {
        Validate(request);

        var coupon = await _db.Coupons.SingleOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Coupon {id} was not found.");

        var code = CouponService.Normalize(request.Code);
        if (await _db.Coupons.AnyAsync(c => c.Code == code && c.Id != id))
            throw new BusinessRuleException("Duplicate code", $"A coupon with code {code} already exists.");

        if (request.UsageLimit.HasValue && request.UsageLimit < coupon.TimesUsed)
            throw new BusinessRuleException("Usage limit too low", $"This coupon has already been used {coupon.TimesUsed} times.", StatusCodes.Status400BadRequest);

        coupon.Code = code;
        Apply(coupon, request);
        await _db.SaveChangesAsync();

        return Ok(ToDto(coupon));
    }

    // Coupons that were used stay in the database so past orders keep their link
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var coupon = await _db.Coupons.SingleOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Coupon {id} was not found.");

        if (coupon.TimesUsed > 0)
            throw new BusinessRuleException("Coupon has been used", "This coupon was used on orders and can't be deleted. Deactivate it instead.");

        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static void Validate(CouponRequest request)
    {
        if (request.DiscountType == Entities.DiscountType.Percentage && request.DiscountValue > 100)
            throw new BusinessRuleException("Invalid discount", "A percentage discount can't be more than 100.", StatusCodes.Status400BadRequest);

        if (request.StartsAt.HasValue && request.ExpiresAt.HasValue && request.ExpiresAt <= request.StartsAt)
            throw new BusinessRuleException("Invalid dates", "The expiry date must be after the start date.", StatusCodes.Status400BadRequest);
    }

    private static void Apply(Coupon coupon, CouponRequest request)
    {
        coupon.Description = request.Description?.Trim();
        coupon.DiscountType = request.DiscountType;
        coupon.DiscountValue = request.DiscountValue;
        coupon.MinOrderAmount = request.MinOrderAmount;
        coupon.MaxDiscountAmount = request.MaxDiscountAmount;
        coupon.StartsAt = request.StartsAt;
        coupon.ExpiresAt = request.ExpiresAt;
        coupon.UsageLimit = request.UsageLimit;
        coupon.UsageLimitPerCustomer = request.UsageLimitPerCustomer;
        coupon.IsActive = request.IsActive;
    }

    private static AdminCouponDto ToDto(Coupon coupon) => new()
    {
        Id = coupon.Id,
        Code = coupon.Code,
        Description = coupon.Description,
        DiscountType = coupon.DiscountType.ToString(),
        DiscountValue = coupon.DiscountValue,
        MinOrderAmount = coupon.MinOrderAmount,
        MaxDiscountAmount = coupon.MaxDiscountAmount,
        StartsAt = coupon.StartsAt,
        ExpiresAt = coupon.ExpiresAt,
        UsageLimit = coupon.UsageLimit,
        UsageLimitPerCustomer = coupon.UsageLimitPerCustomer,
        TimesUsed = coupon.TimesUsed,
        IsActive = coupon.IsActive
    };
}
