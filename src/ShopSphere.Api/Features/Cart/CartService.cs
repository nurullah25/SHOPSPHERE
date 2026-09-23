using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Cart;

public class CartService
{
    public const int MaxQuantityPerItem = 99;
    private const int MaxDistinctItems = 50;

    private readonly AppDbContext _db;

    public CartService(AppDbContext db)
    {
        _db = db;
    }

    // Prices and stock always come from the database, never from the client.
    // Adding to the cart does not reserve stock; that happens at checkout.
    public async Task<CartDto> GetCartAsync(int userId)
    {
        var rows = await _db.CartItems
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new
            {
                c.ProductId,
                c.Product.Name,
                c.Product.Slug,
                ImageUrl = c.Product.Images.Where(i => i.IsMain).Select(i => i.Url).FirstOrDefault(),
                UnitPrice = c.Product.EffectivePrice,
                c.Quantity,
                Stock = c.Product.StockQuantity,
                c.Product.IsActive
            })
            .ToListAsync();

        var items = rows.Select(row =>
        {
            var item = new CartItemDto
            {
                ProductId = row.ProductId,
                Name = row.Name,
                Slug = row.Slug,
                ImageUrl = row.ImageUrl,
                UnitPrice = row.UnitPrice,
                Quantity = row.Quantity,
                LineTotal = row.UnitPrice * row.Quantity,
                AvailableStock = row.Stock,
                IsAvailable = row.IsActive && row.Stock >= row.Quantity
            };

            if (!row.IsActive)
                item.Issue = "This product is no longer available.";
            else if (row.Stock == 0)
                item.Issue = "Out of stock.";
            else if (row.Stock < row.Quantity)
                item.Issue = $"Only {row.Stock} left in stock.";

            return item;
        }).ToList();

        return new CartDto
        {
            Items = items,
            Subtotal = items.Where(i => i.IsAvailable).Sum(i => i.LineTotal),
            ItemCount = items.Sum(i => i.Quantity),
            HasUnavailableItems = items.Any(i => !i.IsAvailable)
        };
    }

    public async Task<CartDto> AddItemAsync(int userId, AddToCartRequest request)
    {
        var product = await _db.Products.SingleOrDefaultAsync(p => p.Id == request.ProductId)
            ?? throw new NotFoundException($"Product {request.ProductId} was not found.");

        var existing = await _db.CartItems.SingleOrDefaultAsync(c => c.UserId == userId && c.ProductId == product.Id);
        var quantity = (existing?.Quantity ?? 0) + request.Quantity;

        if (quantity > MaxQuantityPerItem)
            throw new BusinessRuleException("Quantity too high", $"You can order at most {MaxQuantityPerItem} of the same product.", StatusCodes.Status400BadRequest);

        EnsureAvailable(product, quantity);

        if (existing == null)
        {
            var distinctItems = await _db.CartItems.CountAsync(c => c.UserId == userId);
            if (distinctItems >= MaxDistinctItems)
                throw new BusinessRuleException("Cart is full", $"A cart can hold at most {MaxDistinctItems} different products.", StatusCodes.Status400BadRequest);

            _db.CartItems.Add(new CartItem { UserId = userId, ProductId = product.Id, Quantity = quantity });
        }
        else
        {
            existing.Quantity = quantity;
        }

        await _db.SaveChangesAsync();
        return await GetCartAsync(userId);
    }

    public async Task<CartDto> UpdateQuantityAsync(int userId, int productId, int quantity)
    {
        var item = await _db.CartItems
            .Include(c => c.Product)
            .SingleOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId)
            ?? throw new NotFoundException("This product is not in your cart.");

        EnsureAvailable(item.Product, quantity);

        item.Quantity = quantity;
        await _db.SaveChangesAsync();

        return await GetCartAsync(userId);
    }

    public async Task<CartDto> RemoveItemAsync(int userId, int productId)
    {
        await _db.CartItems
            .Where(c => c.UserId == userId && c.ProductId == productId)
            .ExecuteDeleteAsync();

        return await GetCartAsync(userId);
    }

    public async Task ClearAsync(int userId)
    {
        await _db.CartItems.Where(c => c.UserId == userId).ExecuteDeleteAsync();
    }

    private static void EnsureAvailable(Product product, int quantity)
    {
        if (!product.IsActive)
            throw new BusinessRuleException("Product unavailable", $"{product.Name} is no longer available.", StatusCodes.Status400BadRequest);

        if (product.StockQuantity == 0)
            throw new BusinessRuleException("Out of stock", $"{product.Name} is out of stock.");

        if (quantity > product.StockQuantity)
            throw new BusinessRuleException("Not enough stock", $"Only {product.StockQuantity} of {product.Name} are available.");
    }
}
