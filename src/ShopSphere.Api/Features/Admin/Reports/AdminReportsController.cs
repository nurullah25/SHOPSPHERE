using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;

namespace ShopSphere.Api.Features.Admin.Reports;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Policy = Policies.Admin)]
public class AdminReportsController : ControllerBase
{
    private readonly ReportService _reportService;

    public AdminReportsController(ReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("sales-by-date")]
    public async Task<ActionResult<List<SalesByPeriodDto>>> SalesByDate([FromQuery] ReportRange range)
    {
        return Ok(await _reportService.GetSalesByDateAsync(range));
    }

    [HttpGet("sales-by-product")]
    public async Task<ActionResult<List<SalesByProductDto>>> SalesByProduct([FromQuery] ReportRange range)
    {
        return Ok(await _reportService.GetSalesByProductAsync(range));
    }

    [HttpGet("sales-by-category")]
    public async Task<ActionResult<List<SalesByCategoryDto>>> SalesByCategory([FromQuery] ReportRange range)
    {
        return Ok(await _reportService.GetSalesByCategoryAsync(range));
    }

    [HttpGet("order-status")]
    public async Task<ActionResult<List<OrderStatusSummaryDto>>> OrderStatus([FromQuery] ReportRange range)
    {
        return Ok(await _reportService.GetOrderStatusSummaryAsync(range));
    }
}
