using System.ComponentModel.DataAnnotations;
using ShopSphere.Api.Features.Checkout;
using ShopSphere.Api.Features.Payments;

namespace ShopSphere.Api.Features.Orders;

public class OrderDto
{
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime PlacedAt { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? CouponCode { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }

    public string PaymentStatus { get; set; } = string.Empty;
    public string? PaymentFailureReason { get; set; }

    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
    public SavedAddressDto ShippingAddress { get; set; } = null!;
    public List<OrderStatusEntryDto> StatusHistory { get; set; } = new();
}

public class OrderItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderStatusEntryDto
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class RetryPaymentRequest
{
    [Required]
    [RegularExpression($"^({MockPaymentGateway.SuccessToken}|{MockPaymentGateway.DeclinedToken}|{MockPaymentGateway.InsufficientFundsToken})$",
        ErrorMessage = "Unknown payment method.")]
    public string PaymentToken { get; set; } = string.Empty;
}
