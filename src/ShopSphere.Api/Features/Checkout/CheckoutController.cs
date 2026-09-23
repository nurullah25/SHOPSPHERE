using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;

namespace ShopSphere.Api.Features.Checkout;

[ApiController]
[Route("api/checkout")]
[Authorize(Policy = Policies.Customer)]
public class CheckoutController : ControllerBase
{
    private readonly CheckoutService _checkoutService;

    public CheckoutController(CheckoutService checkoutService)
    {
        _checkoutService = checkoutService;
    }

    [HttpPost("summary")]
    public async Task<ActionResult<CheckoutSummaryDto>> GetSummary(CheckoutSummaryRequest request)
    {
        return Ok(await _checkoutService.GetSummaryAsync(User.GetUserId(), request.CouponCode));
    }

    [HttpPost]
    public async Task<ActionResult<CheckoutResultDto>> PlaceOrder(PlaceOrderRequest request)
    {
        return Ok(await _checkoutService.PlaceOrderAsync(User.GetUserId(), request));
    }
}
