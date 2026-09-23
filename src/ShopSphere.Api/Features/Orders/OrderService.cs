using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Checkout;

namespace ShopSphere.Api.Features.Orders;

public class OrderService
{
    private readonly AppDbContext _db;

    public OrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<OrderDto> GetOrderAsync(int userId, string orderNumber)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.StatusHistory)
            .SingleOrDefaultAsync(o => o.OrderNumber == orderNumber && o.UserId == userId)
            ?? throw new NotFoundException($"Order {orderNumber} was not found.");

        // Product data is only used for links and thumbnails. The order itself
        // keeps its own copy of name, SKU and price.
        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Slug,
                ImageUrl = p.Images.Where(i => i.IsMain).Select(i => i.Url).FirstOrDefault()
            })
            .ToDictionaryAsync(p => p.Id);

        var latestPayment = order.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault();

        return new OrderDto
        {
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            PlacedAt = order.PlacedAt,
            Subtotal = order.Subtotal,
            DiscountAmount = order.DiscountAmount,
            CouponCode = order.CouponCode,
            ShippingCost = order.ShippingCost,
            Total = order.Total,
            PaymentStatus = (latestPayment?.Status ?? PaymentStatus.Pending).ToString(),
            PaymentFailureReason = latestPayment?.FailureReason,
            Notes = order.Notes,
            CancellationReason = order.CancellationReason,
            Items = order.Items.Select(item => new OrderItemDto
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Sku = item.Sku,
                Slug = products.GetValueOrDefault(item.ProductId)?.Slug,
                ImageUrl = products.GetValueOrDefault(item.ProductId)?.ImageUrl,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                LineTotal = item.LineTotal
            }).ToList(),
            ShippingAddress = new SavedAddressDto
            {
                FullName = order.ShippingAddress.FullName,
                Line1 = order.ShippingAddress.Line1,
                Line2 = order.ShippingAddress.Line2,
                City = order.ShippingAddress.City,
                State = order.ShippingAddress.State,
                PostalCode = order.ShippingAddress.PostalCode,
                Country = order.ShippingAddress.Country,
                PhoneNumber = order.ShippingAddress.PhoneNumber
            },
            StatusHistory = order.StatusHistory
                .OrderBy(h => h.ChangedAt)
                .Select(h => new OrderStatusEntryDto
                {
                    FromStatus = h.FromStatus?.ToString(),
                    ToStatus = h.ToStatus.ToString(),
                    Note = h.Note,
                    ChangedAt = h.ChangedAt
                })
                .ToList()
        };
    }
}
