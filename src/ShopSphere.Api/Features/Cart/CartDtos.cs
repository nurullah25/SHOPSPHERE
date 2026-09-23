using System.ComponentModel.DataAnnotations;

namespace ShopSphere.Api.Features.Cart;

public class CartDto
{
    public List<CartItemDto> Items { get; set; } = new();

    // Only counts items that can actually be bought right now
    public decimal Subtotal { get; set; }
    public int ItemCount { get; set; }
    public bool HasUnavailableItems { get; set; }
}

public class CartItemDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public int AvailableStock { get; set; }
    public bool IsAvailable { get; set; }

    // Filled in when the product changed after it was added to the cart
    public string? Issue { get; set; }
}

public class AddToCartRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, CartService.MaxQuantityPerItem)]
    public int Quantity { get; set; } = 1;
}

public class UpdateCartItemRequest
{
    [Range(1, CartService.MaxQuantityPerItem)]
    public int Quantity { get; set; }
}
