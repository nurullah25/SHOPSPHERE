using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;

namespace ShopSphere.Api.Features.Admin.Orders;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Policy = Policies.Admin)]
public class AdminOrdersController : ControllerBase
{
    private readonly AdminOrderService _orderService;

    public AdminOrdersController(AdminOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminOrderListItemDto>>> GetOrders([FromQuery] AdminOrderQuery query)
    {
        return Ok(await _orderService.GetOrdersAsync(query));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminOrderDto>> GetOrder(int id)
    {
        return Ok(await _orderService.GetOrderAsync(id));
    }

    [HttpPost("{id:int}/status")]
    public async Task<ActionResult<AdminOrderDto>> ChangeStatus(int id, ChangeOrderStatusRequest request)
    {
        return Ok(await _orderService.ChangeStatusAsync(id, request, User.GetUserId()));
    }
}
