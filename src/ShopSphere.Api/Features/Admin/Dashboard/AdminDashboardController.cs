using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;

namespace ShopSphere.Api.Features.Admin.Dashboard;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = Policies.Admin)]
public class AdminDashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;

    public AdminDashboardController(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get([FromQuery, Range(1, 365)] int days = 30)
    {
        return Ok(await _dashboardService.GetDashboardAsync(days));
    }
}
