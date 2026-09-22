namespace ShopSphere.Api.Entities;

public class InventoryMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int QuantityChange { get; set; }
    public int QuantityAfter { get; set; }
    public InventoryChangeReason Reason { get; set; }
    public string? Note { get; set; }

    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public DateTime CreatedAt { get; set; }
}
