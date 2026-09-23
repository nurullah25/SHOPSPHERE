using System.ComponentModel.DataAnnotations;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Admin.Inventory;

public class InventoryQuery
{
    [MaxLength(100)]
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    public bool? LowStock { get; set; }

    [RegularExpression("^(name|stock_asc|stock_desc)$")]
    public string Sort { get; set; } = "stock_asc";

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 25;
}

public class InventoryItemDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; }

    // Stock that sits in orders which are placed but not paid yet
    public int ReservedForPendingOrders { get; set; }

    public bool IsLowStock { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class StockAdjustmentRequest
{
    // Positive to add stock, negative to take it out
    [Range(-100_000, 100_000)]
    public int QuantityChange { get; set; }

    [Required]
    public InventoryChangeReason Reason { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class InventoryMovementDto
{
    public int Id { get; set; }
    public int QuantityChange { get; set; }
    public int QuantityAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? OrderNumber { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
