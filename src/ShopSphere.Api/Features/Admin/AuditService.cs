using System.Text.Json;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Admin;

// Adds an audit row to the current DbContext. It's saved together with the
// change it describes, so the log and the data can't get out of sync.
public class AuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public void Record(int userId, string action, string entityName, object entityId, object? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId.ToString()!,
            Details = details == null ? null : JsonSerializer.Serialize(details)
        });
    }
}
