namespace ShopSphere.Api.Entities;

public class AuditLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public string Action { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; }
}
