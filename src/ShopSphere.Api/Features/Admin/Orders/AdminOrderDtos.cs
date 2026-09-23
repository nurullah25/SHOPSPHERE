using System.ComponentModel.DataAnnotations;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Orders;

namespace ShopSphere.Api.Features.Admin.Orders;

public class AdminOrderQuery
{
    public OrderStatus? Status { get; set; }

    // Order number, customer name or email
    [MaxLength(100)]
    public string? Search { get; set; }

    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public class AdminOrderListItemDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime PlacedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int ItemCount { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
}

public class AdminOrderDto : OrderDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public List<string> AllowedNextStatuses { get; set; } = new();
}

public class ChangeOrderStatusRequest
{
    [Required]
    public OrderStatus Status { get; set; }

    [MaxLength(300)]
    public string? Note { get; set; }
}
