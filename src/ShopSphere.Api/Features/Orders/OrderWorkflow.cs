using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Orders;

// Applies a status change and its side effects. Callers load the order with
// its items and run this inside a transaction, then save.
public class OrderWorkflow
{
    private readonly AppDbContext _db;
    private readonly ILogger<OrderWorkflow> _logger;

    public OrderWorkflow(AppDbContext db, ILogger<OrderWorkflow> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ChangeStatusAsync(
        Order order,
        OrderStatus target,
        int? changedByUserId,
        string? note,
        InventoryChangeReason restoreReason = InventoryChangeReason.OrderCancelled)
    {
        OrderStatusRules.EnsureTransition(order.Status, target);

        var now = DateTime.UtcNow;

        if (target == OrderStatus.Cancelled)
        {
            await RestoreStockAsync(order, changedByUserId, restoreReason);
            await ReleaseCouponAsync(order);
            RefundSucceededPayments(order, now);

            order.CancelledAt = now;
            order.CancellationReason = note;
        }

        order.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = order.Status,
            ToStatus = target,
            Note = note,
            ChangedByUserId = changedByUserId,
            ChangedAt = now
        });

        _logger.LogInformation("Order {OrderNumber} moved from {From} to {To}", order.OrderNumber, order.Status, target);
        order.Status = target;
    }

    private async Task RestoreStockAsync(Order order, int? changedByUserId, InventoryChangeReason reason)
    {
        foreach (var item in order.Items)
        {
            await _db.Products
                .Where(p => p.Id == item.ProductId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.StockQuantity, p => p.StockQuantity + item.Quantity));
        }

        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var stockAfter = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.StockQuantity })
            .ToDictionaryAsync(p => p.Id, p => p.StockQuantity);

        foreach (var item in order.Items)
        {
            _db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = item.ProductId,
                QuantityChange = item.Quantity,
                QuantityAfter = stockAfter[item.ProductId],
                Reason = reason,
                OrderId = order.Id,
                UserId = changedByUserId,
                Note = $"Order {order.OrderNumber} cancelled"
            });
        }
    }

    // A cancelled order shouldn't consume one of the coupon's uses
    private async Task ReleaseCouponAsync(Order order)
    {
        if (order.CouponId == null)
            return;

        await _db.Coupons
            .Where(c => c.Id == order.CouponId && c.TimesUsed > 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.TimesUsed, c => c.TimesUsed - 1));

        await _db.CouponRedemptions
            .Where(r => r.OrderId == order.Id)
            .ExecuteDeleteAsync();
    }

    private static void RefundSucceededPayments(Order order, DateTime now)
    {
        foreach (var payment in order.Payments.Where(p => p.Status == PaymentStatus.Succeeded))
        {
            payment.Status = PaymentStatus.Refunded;
            payment.ProcessedAt = now;
        }
    }
}
