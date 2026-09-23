using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Coupons;
using ShopSphere.Api.Features.Payments;

namespace ShopSphere.Api.Features.Checkout;

public class CheckoutService
{
    private readonly AppDbContext _db;
    private readonly CouponService _couponService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ShippingSettings _shipping;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        AppDbContext db,
        CouponService couponService,
        IPaymentGateway paymentGateway,
        IOptions<ShippingSettings> shipping,
        ILogger<CheckoutService> logger)
    {
        _db = db;
        _couponService = couponService;
        _paymentGateway = paymentGateway;
        _shipping = shipping.Value;
        _logger = logger;
    }

    public async Task<CheckoutSummaryDto> GetSummaryAsync(int userId, string? couponCode)
    {
        var cartItems = await LoadCartAsync(userId);

        var summary = new CheckoutSummaryDto
        {
            Items = cartItems.Select(item => new CheckoutItemDto
            {
                ProductId = item.ProductId,
                Name = item.Product.Name,
                Slug = item.Product.Slug,
                UnitPrice = item.Product.EffectivePrice,
                Quantity = item.Quantity,
                LineTotal = item.Product.EffectivePrice * item.Quantity
            }).ToList(),
            FreeShippingThreshold = _shipping.FreeShippingThreshold,
            SavedAddresses = await GetSavedAddressesAsync(userId)
        };

        foreach (var item in cartItems)
        {
            if (!item.Product.IsActive)
                summary.Issues.Add($"{item.Product.Name} is no longer available.");
            else if (item.Product.StockQuantity < item.Quantity)
                summary.Issues.Add($"Only {item.Product.StockQuantity} of {item.Product.Name} are left.");
        }

        summary.Subtotal = summary.Items.Sum(i => i.LineTotal);

        if (!string.IsNullOrWhiteSpace(couponCode) && summary.Issues.Count == 0)
        {
            var evaluation = await _couponService.EvaluateAsync(couponCode, summary.Subtotal, userId);
            summary.DiscountAmount = evaluation.Discount;
            summary.CouponCode = evaluation.Coupon.Code;
            summary.CouponDescription = evaluation.Coupon.Description;
        }

        summary.ShippingCost = _shipping.CalculateFor(summary.Subtotal - summary.DiscountAmount);
        summary.Total = summary.Subtotal - summary.DiscountAmount + summary.ShippingCost;

        // Product images are loaded separately to keep the projection simple
        var imageUrls = await _db.ProductImages
            .Where(i => i.IsMain && summary.Items.Select(x => x.ProductId).Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, i => i.Url);

        foreach (var item in summary.Items)
            item.ImageUrl = imageUrls.GetValueOrDefault(item.ProductId);

        return summary;
    }

    public async Task<CheckoutResultDto> PlaceOrderAsync(int userId, PlaceOrderRequest request)
    {
        var cartItems = await LoadCartAsync(userId);
        if (cartItems.Count == 0)
            throw new BusinessRuleException("Cart is empty", "Add something to your cart before checking out.", StatusCodes.Status400BadRequest);

        // Friendly, early validation. The real guarantee is the conditional
        // UPDATE inside the transaction below.
        foreach (var item in cartItems)
        {
            if (!item.Product.IsActive)
                throw new BusinessRuleException("Product unavailable", $"{item.Product.Name} is no longer available.", StatusCodes.Status400BadRequest);

            if (item.Product.StockQuantity < item.Quantity)
                throw new BusinessRuleException("Not enough stock", $"Only {item.Product.StockQuantity} of {item.Product.Name} are left.");
        }

        var subtotal = cartItems.Sum(i => i.Product.EffectivePrice * i.Quantity);

        CouponEvaluation? coupon = null;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
            coupon = await _couponService.EvaluateAsync(request.CouponCode, subtotal, userId);

        var discount = coupon?.Discount ?? 0m;
        var shippingCost = _shipping.CalculateFor(subtotal - discount);
        var total = subtotal - discount + shippingCost;

        var address = await ResolveAddressAsync(userId, request);

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Pending,
            Subtotal = subtotal,
            DiscountAmount = discount,
            ShippingCost = shippingCost,
            Total = total,
            CouponId = coupon?.Coupon.Id,
            CouponCode = coupon?.Coupon.Code,
            ShippingAddress = address,
            Notes = request.Notes,
            PlacedAt = DateTime.UtcNow
        };

        await using var transaction = await _db.Database.BeginTransactionAsync();

        foreach (var item in cartItems)
        {
            // Check and decrement in one statement. Two customers buying the
            // last unit can't both succeed: the second one updates 0 rows.
            var rowsUpdated = await _db.Products
                .Where(p => p.Id == item.ProductId && p.IsActive && p.StockQuantity >= item.Quantity)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.StockQuantity, p => p.StockQuantity - item.Quantity));

            if (rowsUpdated == 0)
                throw new BusinessRuleException("Not enough stock", $"{item.Product.Name} just sold out. Please update your cart.");
        }

        if (coupon != null)
        {
            var rowsUpdated = await _db.Coupons
                .Where(c => c.Id == coupon.Coupon.Id && (c.UsageLimit == null || c.TimesUsed < c.UsageLimit))
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.TimesUsed, c => c.TimesUsed + 1));

            if (rowsUpdated == 0)
                throw new BusinessRuleException("Coupon unavailable", "This coupon just reached its usage limit.");
        }

        // Stock levels after the decrements, used for the inventory ledger
        var productIds = cartItems.Select(i => i.ProductId).ToList();
        var stockAfter = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.StockQuantity })
            .ToDictionaryAsync(p => p.Id, p => p.StockQuantity);

        foreach (var item in cartItems)
        {
            // Price, name and SKU are copied so the order keeps what was paid
            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                Sku = item.Product.Sku,
                UnitPrice = item.Product.EffectivePrice,
                Quantity = item.Quantity,
                LineTotal = item.Product.EffectivePrice * item.Quantity
            });

            _db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = item.ProductId,
                QuantityChange = -item.Quantity,
                QuantityAfter = stockAfter[item.ProductId],
                Reason = InventoryChangeReason.Sale,
                Order = order,
                UserId = userId
            });
        }

        order.StatusHistory.Add(new OrderStatusHistory
        {
            ToStatus = OrderStatus.Pending,
            Note = "Order placed",
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow
        });

        var payment = new Payment
        {
            Amount = total,
            Status = PaymentStatus.Pending,
            Provider = MockPaymentGateway.Provider
        };
        order.Payments.Add(payment);

        if (coupon != null)
        {
            _db.CouponRedemptions.Add(new CouponRedemption
            {
                CouponId = coupon.Coupon.Id,
                UserId = userId,
                Order = order
            });
        }

        _db.Orders.Add(order);
        _db.CartItems.RemoveRange(cartItems);

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.LogInformation("Order {OrderNumber} placed by user {UserId} for {Total}", order.OrderNumber, userId, total);

        // Payment happens after the commit: an external call should never be
        // made while database locks are held.
        return await ChargeAsync(order, payment, request.PaymentToken);
    }

    public async Task<CheckoutResultDto> RetryPaymentAsync(int userId, string orderNumber, string paymentToken)
    {
        var order = await _db.Orders
            .Include(o => o.Payments)
            .SingleOrDefaultAsync(o => o.OrderNumber == orderNumber && o.UserId == userId)
            ?? throw new NotFoundException($"Order {orderNumber} was not found.");

        if (order.Status != OrderStatus.Pending)
            throw new BusinessRuleException("Payment not needed", $"This order is already {order.Status.ToString().ToLowerInvariant()}.");

        var payment = new Payment
        {
            Amount = order.Total,
            Status = PaymentStatus.Pending,
            Provider = MockPaymentGateway.Provider
        };
        order.Payments.Add(payment);
        await _db.SaveChangesAsync();

        return await ChargeAsync(order, payment, paymentToken);
    }

    private async Task<CheckoutResultDto> ChargeAsync(Order order, Payment payment, string paymentToken)
    {
        var result = await _paymentGateway.ChargeAsync(new PaymentCharge(order.OrderNumber, order.Total, paymentToken));

        payment.TransactionReference = result.TransactionReference;
        payment.ProcessedAt = DateTime.UtcNow;

        if (result.Succeeded)
        {
            payment.Status = PaymentStatus.Succeeded;
            order.Status = OrderStatus.Confirmed;
            order.StatusHistory.Add(new OrderStatusHistory
            {
                FromStatus = OrderStatus.Pending,
                ToStatus = OrderStatus.Confirmed,
                Note = "Payment received",
                ChangedAt = DateTime.UtcNow
            });
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = result.FailureReason;
            _logger.LogWarning("Payment failed for order {OrderNumber}: {Reason}", order.OrderNumber, result.FailureReason);
        }

        await _db.SaveChangesAsync();

        return new CheckoutResultDto
        {
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            PaymentStatus = payment.Status.ToString(),
            Total = order.Total,
            PaymentSucceeded = result.Succeeded,
            Message = result.Succeeded
                ? "Payment received. Your order is confirmed."
                : $"{result.FailureReason} Your order is reserved for 30 minutes so you can try again."
        };
    }

    private Task<List<CartItem>> LoadCartAsync(int userId) =>
        _db.CartItems
            .Include(c => c.Product)
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

    private Task<List<SavedAddressDto>> GetSavedAddressesAsync(int userId) =>
        _db.Addresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .Select(a => new SavedAddressDto
            {
                Id = a.Id,
                FullName = a.FullName,
                Line1 = a.Line1,
                Line2 = a.Line2,
                City = a.City,
                State = a.State,
                PostalCode = a.PostalCode,
                Country = a.Country,
                PhoneNumber = a.PhoneNumber,
                IsDefault = a.IsDefault
            })
            .ToListAsync();

    private async Task<OrderAddress> ResolveAddressAsync(int userId, PlaceOrderRequest request)
    {
        if (request.AddressId.HasValue)
        {
            var saved = await _db.Addresses
                .SingleOrDefaultAsync(a => a.Id == request.AddressId && a.UserId == userId)
                ?? throw new NotFoundException("That address was not found.");

            return new OrderAddress
            {
                FullName = saved.FullName,
                Line1 = saved.Line1,
                Line2 = saved.Line2,
                City = saved.City,
                State = saved.State,
                PostalCode = saved.PostalCode,
                Country = saved.Country,
                PhoneNumber = saved.PhoneNumber
            };
        }

        var address = request.ShippingAddress
            ?? throw new BusinessRuleException("Address required", "Choose a saved address or enter a new one.", StatusCodes.Status400BadRequest);

        if (address.SaveToAddressBook)
        {
            var hasAddresses = await _db.Addresses.AnyAsync(a => a.UserId == userId);
            _db.Addresses.Add(new Address
            {
                UserId = userId,
                FullName = address.FullName,
                Line1 = address.Line1,
                Line2 = address.Line2,
                City = address.City,
                State = address.State,
                PostalCode = address.PostalCode,
                Country = address.Country,
                PhoneNumber = address.PhoneNumber,
                IsDefault = !hasAddresses
            });
        }

        return new OrderAddress
        {
            FullName = address.FullName.Trim(),
            Line1 = address.Line1.Trim(),
            Line2 = address.Line2?.Trim(),
            City = address.City.Trim(),
            State = address.State?.Trim(),
            PostalCode = address.PostalCode.Trim(),
            Country = address.Country.Trim(),
            PhoneNumber = address.PhoneNumber?.Trim()
        };
    }
}
