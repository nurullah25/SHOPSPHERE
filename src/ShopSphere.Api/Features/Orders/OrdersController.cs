using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;
using ShopSphere.Api.Features.Checkout;

namespace ShopSphere.Api.Features.Orders;

[ApiController]
[Route("api/orders")]
[Authorize(Policy = Policies.Customer)]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly CheckoutService _checkoutService;

    public OrdersController(OrderService orderService, CheckoutService checkoutService)
    {
        _orderService = orderService;
        _checkoutService = checkoutService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> GetOrders(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 50)] int pageSize = 10)
    {
        return Ok(await _orderService.GetOrdersAsync(User.GetUserId(), page, pageSize));
    }

    [HttpPost("{orderNumber}/cancel")]
    public async Task<ActionResult<OrderDto>> Cancel(string orderNumber, CancelOrderRequest request)
    {
        return Ok(await _orderService.CancelAsync(User.GetUserId(), orderNumber, request.Reason));
    }

    [HttpGet("{orderNumber}")]
    public async Task<ActionResult<OrderDto>> GetOrder(string orderNumber)
    {
        return Ok(await _orderService.GetOrderAsync(User.GetUserId(), orderNumber));
    }

    // Used when the mock gateway declined the first attempt
    [HttpPost("{orderNumber}/payments")]
    public async Task<ActionResult<CheckoutResultDto>> RetryPayment(string orderNumber, RetryPaymentRequest request)
    {
        return Ok(await _checkoutService.RetryPaymentAsync(User.GetUserId(), orderNumber, request.PaymentToken));
    }
}
