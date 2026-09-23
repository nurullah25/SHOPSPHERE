using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Checkout;
using ShopSphere.Api.Features.Orders;

namespace ShopSphere.Api.Features.Admin.Orders;

public class AdminOrderService
{
    private readonly AppDbContext _db;
    private readonly OrderWorkflow _workflow;
    private readonly AuditService _audit;

    public AdminOrderService(AppDbContext db, OrderWorkflow workflow, AuditService audit)
    {
        _db = db;
        _workflow = workflow;
        _audit = audit;
    }

    public Task<PagedResult<AdminOrderListItemDto>> GetOrdersAsync(AdminOrderQuery query)
    {
        var orders = _db.Orders.AsNoTracking();

        if (query.Status.HasValue)
            orders = orders.Where(o => o.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            orders = orders.Where(o =>
                o.OrderNumber.Contains(term) ||
                o.User.Email.Contains(term) ||
                o.User.FirstName.Contains(term) ||
                o.User.LastName.Contains(term));
        }

        if (query.From.HasValue)
            orders = orders.Where(o => o.PlacedAt >= query.From);

        if (query.To.HasValue)
        {
            // Treat the end date as inclusive
            var to = query.To.Value.Date.AddDays(1);
            orders = orders.Where(o => o.PlacedAt < to);
        }

        return orders
            .OrderByDescending(o => o.PlacedAt).ThenByDescending(o => o.Id)
            .Select(o => new AdminOrderListItemDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                PlacedAt = o.PlacedAt,
                Status = o.Status.ToString(),
                PaymentStatus = o.Payments.OrderByDescending(p => p.CreatedAt).Select(p => p.Status.ToString()).FirstOrDefault() ?? string.Empty,
                Total = o.Total,
                ItemCount = o.Items.Sum(i => i.Quantity),
                CustomerName = o.User.FirstName + " " + o.User.LastName,
                CustomerEmail = o.User.Email
            })
            .ToPagedResultAsync(query.Page, query.PageSize);
    }

    public async Task<AdminOrderDto> GetOrderAsync(int id)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.StatusHistory)
            .Include(o => o.User)
            .SingleOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException($"Order {id} was not found.");

        return ToDto(order);
    }

    public async Task<AdminOrderDto> ChangeStatusAsync(int id, ChangeOrderStatusRequest request, int adminId)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.StatusHistory)
            .Include(o => o.User)
            .SingleOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException($"Order {id} was not found.");

        var previousStatus = order.Status;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        await _workflow.ChangeStatusAsync(order, request.Status, adminId, request.Note);

        _audit.Record(adminId, "OrderStatusChanged", nameof(Order), order.Id, new
        {
            order.OrderNumber,
            From = previousStatus.ToString(),
            To = request.Status.ToString(),
            request.Note
        });

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return ToDto(order);
    }

    private static AdminOrderDto ToDto(Order order)
    {
        var latestPayment = order.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault();

        return new AdminOrderDto
        {
            Id = order.Id,
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
            CustomerId = order.UserId,
            CustomerName = $"{order.User.FirstName} {order.User.LastName}",
            CustomerEmail = order.User.Email,
            AllowedNextStatuses = OrderStatusRules.AllowedNext(order.Status).Select(s => s.ToString()).ToList(),
            Items = order.Items.Select(item => new OrderItemDto
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Sku = item.Sku,
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
