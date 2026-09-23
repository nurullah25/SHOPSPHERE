using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;

namespace ShopSphere.Api.Features.Admin.AuditLogs;

public class AuditLogDto
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Policy = Policies.Admin)]
public class AdminAuditLogsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminAuditLogsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetLogs(
        [FromQuery, MaxLength(100)] string? action,
        [FromQuery, MaxLength(100)] string? entityName,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25)
    {
        var logs = _db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
            logs = logs.Where(a => a.Action == action.Trim());

        if (!string.IsNullOrWhiteSpace(entityName))
            logs = logs.Where(a => a.EntityName == entityName.Trim());

        return Ok(await logs
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                Details = a.Details,
                ChangedBy = a.User != null ? a.User.FirstName + " " + a.User.LastName : null,
                CreatedAt = a.CreatedAt
            })
            .ToPagedResultAsync(page, pageSize));
    }

    // Lets the UI build a filter list without hardcoding action names
    [HttpGet("actions")]
    public async Task<ActionResult<List<string>>> GetActions()
    {
        return Ok(await _db.AuditLogs
            .AsNoTracking()
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(action => action)
            .ToListAsync());
    }
}
