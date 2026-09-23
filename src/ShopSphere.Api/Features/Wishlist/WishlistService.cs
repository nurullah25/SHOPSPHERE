using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Cart;

namespace ShopSphere.Api.Features.Wishlist;

public class WishlistItemDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public bool InStock { get; set; }
    public bool IsActive { get; set; }
    public DateTime AddedAt { get; set; }
}

public class WishlistService
{
    private readonly AppDbContext _db;
    private readonly CartService _cartService;

    public WishlistService(AppDbContext db, CartService cartService)
    {
        _db = db;
        _cartService = cartService;
    }

    public Task<List<WishlistItemDto>> GetItemsAsync(int userId)
    {
        return _db.WishlistItems
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WishlistItemDto
            {
                ProductId = w.ProductId,
                Name = w.Product.Name,
                Slug = w.Product.Slug,
                ImageUrl = w.Product.Images.Where(i => i.IsMain).Select(i => i.Url).FirstOrDefault(),
                Price = w.Product.Price,
                DiscountPrice = w.Product.DiscountPrice,
                InStock = w.Product.StockQuantity > 0,
                IsActive = w.Product.IsActive,
                AddedAt = w.CreatedAt
            })
            .ToListAsync();
    }

    // Adding twice is not an error, the product is simply already saved
    public async Task AddAsync(int userId, int productId)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId))
            throw new NotFoundException($"Product {productId} was not found.");

        if (await _db.WishlistItems.AnyAsync(w => w.UserId == userId && w.ProductId == productId))
            return;

        _db.WishlistItems.Add(new WishlistItem { UserId = userId, ProductId = productId });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveAsync(int userId, int productId)
    {
        await _db.WishlistItems
            .Where(w => w.UserId == userId && w.ProductId == productId)
            .ExecuteDeleteAsync();
    }

    public async Task<CartDto> MoveToCartAsync(int userId, int productId)
    {
        if (!await _db.WishlistItems.AnyAsync(w => w.UserId == userId && w.ProductId == productId))
            throw new NotFoundException("This product is not in your wishlist.");

        // Stock is validated here, so the product only leaves the wishlist
        // when it actually made it into the cart
        var cart = await _cartService.AddItemAsync(userId, new AddToCartRequest { ProductId = productId, Quantity = 1 });
        await RemoveAsync(userId, productId);

        return cart;
    }
}
