using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;
using ShopSphere.Api.Features.Checkout;

namespace ShopSphere.Api.Features.Coupons;

public class ValidateCouponRequest
{
    [Required, MaxLength(40)]
    public string Code { get; set; } = string.Empty;
}

[ApiController]
[Route("api/coupons")]
[Authorize(Policy = Policies.Customer)]
public class CouponsController : ControllerBase
{
    private readonly CheckoutService _checkoutService;

    public CouponsController(CheckoutService checkoutService)
    {
        _checkoutService = checkoutService;
    }

    // Returns the totals the coupon would produce for the current cart.
    // An invalid coupon comes back as a 400 with the reason.
    [HttpPost("validate")]
    public async Task<ActionResult<CheckoutSummaryDto>> Validate(ValidateCouponRequest request)
    {
        return Ok(await _checkoutService.GetSummaryAsync(User.GetUserId(), request.Code));
    }
}
