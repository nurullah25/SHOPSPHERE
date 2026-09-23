using System.ComponentModel.DataAnnotations;
using ShopSphere.Api.Features.Payments;

namespace ShopSphere.Api.Features.Checkout;

public class CheckoutSummaryRequest
{
    [MaxLength(40)]
    public string? CouponCode { get; set; }
}

public class CheckoutSummaryDto
{
    public List<CheckoutItemDto> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? CouponCode { get; set; }
    public string? CouponDescription { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public decimal FreeShippingThreshold { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<SavedAddressDto> SavedAddresses { get; set; } = new();
}

public class CheckoutItemDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class SavedAddressDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsDefault { get; set; }
}

public class ShippingAddressRequest
{
    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Line1 { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Line2 { get; set; }

    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? State { get; set; }

    [Required, MaxLength(20)]
    public string PostalCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Country { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    public bool SaveToAddressBook { get; set; }
}

public class PlaceOrderRequest
{
    [MaxLength(40)]
    public string? CouponCode { get; set; }

    // Either pick a saved address or send a new one
    public int? AddressId { get; set; }

    public ShippingAddressRequest? ShippingAddress { get; set; }

    [Required]
    [RegularExpression($"^({MockPaymentGateway.SuccessToken}|{MockPaymentGateway.DeclinedToken}|{MockPaymentGateway.InsufficientFundsToken})$",
        ErrorMessage = "Unknown payment method.")]
    public string PaymentToken { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class CheckoutResultDto
{
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public bool PaymentSucceeded { get; set; }
    public string Message { get; set; } = string.Empty;
}
