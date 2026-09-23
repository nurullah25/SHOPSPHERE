using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Admin.Inventory;

public class InventoryService
{
    // Sales and cancellations come from the checkout and order workflow,
    // so a manual adjustment can only be one of these
    private static readonly InventoryChangeReason[] ManualReasons =
        [InventoryChangeReason.Restock, InventoryChangeReason.Adjustment];

    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public InventoryService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PagedResult<InventoryItemDto>> GetInventoryAsync(InventoryQuery query)
    {
        var products = _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(p => p.Name.Contains(term) || p.Sku.Contains(term));
        }

        if (query.CategoryId.HasValue)
            products = products.Where(p => p.CategoryId == query.CategoryId);

        if (query.LowStock == true)
            products = products.Where(p => p.StockQuantity <= p.LowStockThreshold);

        IQueryable<Product> sorted = query.Sort switch
        {
            "name" => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
            "stock_desc" => products.OrderByDescending(p => p.StockQuantity).ThenBy(p => p.Id),
            _ => products.OrderBy(p => p.StockQuantity).ThenBy(p => p.Id)
        };

        var page = await sorted
            .Select(p => new InventoryItemDto
            {
                ProductId = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category.Name,
                StockQuantity = p.StockQuantity,
                LowStockThreshold = p.LowStockThreshold,
                IsLowStock = p.StockQuantity <= p.LowStockThreshold,
                IsActive = p.IsActive,
                UpdatedAt = p.UpdatedAt
            })
            .ToPagedResultAsync(query.Page, query.PageSize);

        await AddReservedQuantitiesAsync(page.Items);
        return page;
    }

    public async Task<InventoryMovementDto> AdjustStockAsync(int productId, StockAdjustmentRequest request, int adminId)
    {
        if (request.QuantityChange == 0)
            throw new BusinessRuleException("Nothing to change", "Enter a quantity other than zero.", StatusCodes.Status400BadRequest);

        if (!ManualReasons.Contains(request.Reason))
            throw new BusinessRuleException("Invalid reason", "Manual adjustments must be a restock or a correction.", StatusCodes.Status400BadRequest);

        var product = await _db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId)
            ?? throw new NotFoundException($"Product {productId} was not found.");

        await using var transaction = await _db.Database.BeginTransactionAsync();

        // The condition keeps stock from going negative even if two admins
        // adjust the same product at the same time
        var rowsUpdated = await _db.Products
            .Where(p => p.Id == productId && p.StockQuantity + request.QuantityChange >= 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.StockQuantity, p => p.StockQuantity + request.QuantityChange));

        if (rowsUpdated == 0)
        {
            throw new BusinessRuleException("Not enough stock",
                $"{product.Name} only has {product.StockQuantity} in stock, so it can't go down by {Math.Abs(request.QuantityChange)}.");
        }

        var stockAfter = await _db.Products
            .Where(p => p.Id == productId)
            .Select(p => p.StockQuantity)
            .SingleAsync();

        var movement = new InventoryMovement
        {
            ProductId = productId,
            QuantityChange = request.QuantityChange,
            QuantityAfter = stockAfter,
            Reason = request.Reason,
            Note = request.Note?.Trim(),
            UserId = adminId
        };
        _db.InventoryMovements.Add(movement);

        _audit.Record(adminId, "StockAdjusted", nameof(Product), productId, new
        {
            product.Sku,
            request.QuantityChange,
            StockAfter = stockAfter,
            Reason = request.Reason.ToString(),
            request.Note
        });

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new InventoryMovementDto
        {
            Id = movement.Id,
            QuantityChange = movement.QuantityChange,
            QuantityAfter = movement.QuantityAfter,
            Reason = movement.Reason.ToString(),
            Note = movement.Note,
            CreatedAt = movement.CreatedAt
        };
    }

    public async Task<PagedResult<InventoryMovementDto>> GetMovementsAsync(int productId, int page, int pageSize)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId))
            throw new NotFoundException($"Product {productId} was not found.");

        return await _db.InventoryMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Select(m => new InventoryMovementDto
            {
                Id = m.Id,
                QuantityChange = m.QuantityChange,
                QuantityAfter = m.QuantityAfter,
                Reason = m.Reason.ToString(),
                Note = m.Note,
                OrderNumber = m.Order != null ? m.Order.OrderNumber : null,
                ChangedBy = m.User != null ? m.User.FirstName + " " + m.User.LastName : null,
                CreatedAt = m.CreatedAt
            })
            .ToPagedResultAsync(page, pageSize);
    }

    private async Task AddReservedQuantitiesAsync(IReadOnlyList<InventoryItemDto> items)
    {
        var productIds = items.Select(i => i.ProductId).ToList();

        var reserved = await _db.OrderItems
            .Where(i => productIds.Contains(i.ProductId) && i.Order.Status == OrderStatus.Pending)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);

        foreach (var item in items)
            item.ReservedForPendingOrders = reserved.GetValueOrDefault(item.ProductId);
    }
}
