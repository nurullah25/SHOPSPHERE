using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;

namespace ShopSphere.Api.Features.Admin.Inventory;

[ApiController]
[Route("api/admin/inventory")]
[Authorize(Policy = Policies.Admin)]
public class AdminInventoryController : ControllerBase
{
    private readonly InventoryService _inventoryService;

    public AdminInventoryController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InventoryItemDto>>> GetInventory([FromQuery] InventoryQuery query)
    {
        return Ok(await _inventoryService.GetInventoryAsync(query));
    }

    [HttpPost("{productId:int}/adjustments")]
    public async Task<ActionResult<InventoryMovementDto>> Adjust(int productId, StockAdjustmentRequest request)
    {
        return Ok(await _inventoryService.AdjustStockAsync(productId, request, User.GetUserId()));
    }

    [HttpGet("{productId:int}/movements")]
    public async Task<ActionResult<PagedResult<InventoryMovementDto>>> GetMovements(
        int productId,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20)
    {
        return Ok(await _inventoryService.GetMovementsAsync(productId, page, pageSize));
    }
}
