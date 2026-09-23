using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Admin.Orders;
using ShopSphere.Api.Features.Cart;
using ShopSphere.Api.Features.Checkout;
using ShopSphere.Api.Features.Orders;
using ShopSphere.Api.Features.Payments;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class OrderTests
{
    private readonly ShopSphereApiFactory _factory;

    public OrderTests(ShopSphereApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Order_history_shows_only_the_customers_own_orders()
    {
        var buyer = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 10);
        await BuyAsync(buyer, product.Id);
        await BuyAsync(buyer, product.Id);

        var other = await _factory.CreateCustomerClientAsync();
        await BuyAsync(other, product.Id);

        var history = await buyer.GetFromJsonAsync<PagedResult<OrderSummaryDto>>("/api/orders?page=1&pageSize=10");

        Assert.Equal(2, history!.TotalCount);
        Assert.All(history.Items, order => Assert.Equal("Confirmed", order.Status));
    }

    [Fact]
    public async Task Cancelling_an_order_puts_the_stock_back_and_refunds_the_payment()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 6);
        var order = await BuyAsync(client, product.Id, quantity: 2);

        var stockAfterOrder = await StockAsync(product.Id);
        Assert.Equal(4, stockAfterOrder);

        var response = await client.PostAsJsonAsync($"/api/orders/{order.OrderNumber}/cancel",
            new CancelOrderRequest { Reason = "Ordered the wrong size" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = (await response.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Refunded", cancelled.PaymentStatus);
        Assert.False(cancelled.CanCancel);

        Assert.Equal(6, await StockAsync(product.Id));

        var movement = await _factory.WithDbAsync(db => db.InventoryMovements
            .SingleAsync(m => m.ProductId == product.Id && m.Reason == InventoryChangeReason.OrderCancelled));
        Assert.Equal(2, movement.QuantityChange);
        Assert.Equal(6, movement.QuantityAfter);
    }

    [Fact]
    public async Task Cancelling_gives_the_coupon_use_back()
    {
        var code = $"GIVEBACK{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        await _factory.WithDbAsync(async db =>
        {
            db.Coupons.Add(new Coupon
            {
                Code = code,
                DiscountType = DiscountType.FixedAmount,
                DiscountValue = 10,
                UsageLimit = 1
            });
            return await db.SaveChangesAsync();
        });

        var client = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 5);
        var order = await BuyAsync(client, product.Id, couponCode: code);

        Assert.Equal(1, await CouponUsesAsync(code));

        await client.PostAsJsonAsync($"/api/orders/{order.OrderNumber}/cancel", new CancelOrderRequest());

        Assert.Equal(0, await CouponUsesAsync(code));
        var redemptions = await _factory.WithDbAsync(db => db.CouponRedemptions.CountAsync(r => r.Coupon.Code == code));
        Assert.Equal(0, redemptions);
    }

    [Fact]
    public async Task Customer_cannot_cancel_an_order_that_is_already_being_processed()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(stock: 5);
        var order = await BuyAsync(client, product.Id);

        var orderId = await OrderIdAsync(order.OrderNumber);
        await ChangeStatusAsync(admin, orderId, OrderStatus.Processing);

        var response = await client.PostAsJsonAsync($"/api/orders/{order.OrderNumber}/cancel", new CancelOrderRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_cancelled_order_cannot_be_shipped()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(stock: 5);
        var order = await BuyAsync(client, product.Id);
        await client.PostAsJsonAsync($"/api/orders/{order.OrderNumber}/cancel", new CancelOrderRequest());

        var orderId = await OrderIdAsync(order.OrderNumber);
        var response = await ChangeStatusAsync(admin, orderId, OrderStatus.Shipped);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var status = await _factory.WithDbAsync(db => db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync());
        Assert.Equal(OrderStatus.Cancelled, status);
    }

    [Fact]
    public async Task Admin_moves_an_order_through_the_allowed_statuses()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(stock: 5);
        var order = await BuyAsync(client, product.Id);
        var orderId = await OrderIdAsync(order.OrderNumber);

        await ChangeStatusAsync(admin, orderId, OrderStatus.Processing);
        await ChangeStatusAsync(admin, orderId, OrderStatus.Shipped);
        var delivered = await ChangeStatusAsync(admin, orderId, OrderStatus.Delivered);

        var dto = (await delivered.Content.ReadFromJsonAsync<AdminOrderDto>())!;
        Assert.Equal("Delivered", dto.Status);
        Assert.Empty(dto.AllowedNextStatuses);
        // Pending, Confirmed, Processing, Shipped, Delivered
        Assert.Equal(5, dto.StatusHistory.Count);
    }

    [Fact]
    public async Task Admin_cancelling_an_order_restores_stock_and_is_written_to_the_audit_log()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(stock: 8);
        var order = await BuyAsync(client, product.Id, quantity: 3);
        var orderId = await OrderIdAsync(order.OrderNumber);

        await ChangeStatusAsync(admin, orderId, OrderStatus.Cancelled, "Out of stock in the warehouse");

        Assert.Equal(8, await StockAsync(product.Id));

        var log = await _factory.WithDbAsync(db => db.AuditLogs
            .SingleAsync(a => a.Action == "OrderStatusChanged" && a.EntityId == orderId.ToString()));
        Assert.Contains("Cancelled", log.Details);
    }

    [Fact]
    public async Task Admin_order_list_can_be_filtered_by_status_and_customer()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(stock: 5);
        var order = await BuyAsync(client, product.Id);

        var email = await _factory.WithDbAsync(db => db.Orders
            .Where(o => o.OrderNumber == order.OrderNumber)
            .Select(o => o.User.Email)
            .SingleAsync());

        var byCustomer = await admin.GetFromJsonAsync<PagedResult<AdminOrderListItemDto>>($"/api/admin/orders?search={email}");
        var cancelledOnly = await admin.GetFromJsonAsync<PagedResult<AdminOrderListItemDto>>($"/api/admin/orders?search={email}&status=Cancelled");

        Assert.Single(byCustomer!.Items);
        Assert.Equal(order.OrderNumber, byCustomer.Items[0].OrderNumber);
        Assert.Empty(cancelledOnly!.Items);
    }

    [Fact]
    public async Task Status_can_be_sent_as_a_name_like_the_client_does()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(stock: 5);
        var order = await BuyAsync(client, product.Id);
        var orderId = await OrderIdAsync(order.OrderNumber);

        var body = new StringContent("""{"status":"Processing","note":"Sent as a string"}""", Encoding.UTF8, "application/json");
        var response = await admin.PostAsync($"/api/admin/orders/{orderId}/status", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<AdminOrderDto>())!;
        Assert.Equal("Processing", dto.Status);
    }

    [Fact]
    public async Task Customers_cannot_reach_admin_order_endpoints()
    {
        var client = await _factory.CreateCustomerClientAsync();

        var list = await client.GetAsync("/api/admin/orders");
        var change = await client.PostAsJsonAsync("/api/admin/orders/1/status", new ChangeOrderStatusRequest { Status = OrderStatus.Shipped });

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, change.StatusCode);
    }

    private static Task<HttpResponseMessage> ChangeStatusAsync(HttpClient admin, int orderId, OrderStatus status, string? note = null) =>
        admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/status", new ChangeOrderStatusRequest { Status = status, Note = note });

    private static async Task<CheckoutResultDto> BuyAsync(HttpClient client, int productId, int quantity = 1, string? couponCode = null)
    {
        var add = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = productId, Quantity = quantity });
        add.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/checkout", new PlaceOrderRequest
        {
            CouponCode = couponCode,
            PaymentToken = MockPaymentGateway.SuccessToken,
            ShippingAddress = new ShippingAddressRequest
            {
                FullName = "Test Customer",
                Line1 = "742 Maple Avenue",
                City = "Portland",
                PostalCode = "97205",
                Country = "United States"
            }
        });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CheckoutResultDto>())!;
    }

    private Task<int> StockAsync(int productId) =>
        _factory.WithDbAsync(db => db.Products.Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync());

    private Task<int> CouponUsesAsync(string code) =>
        _factory.WithDbAsync(db => db.Coupons.Where(c => c.Code == code).Select(c => c.TimesUsed).SingleAsync());

    private Task<int> OrderIdAsync(string orderNumber) =>
        _factory.WithDbAsync(db => db.Orders.Where(o => o.OrderNumber == orderNumber).Select(o => o.Id).SingleAsync());

    private Task<Product> CreateProductAsync(int stock) => _factory.WithDbAsync(async db =>
    {
        var categoryId = await db.Categories.Where(c => c.Slug == "cookware").Select(c => c.Id).SingleAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var product = new Product
        {
            CategoryId = categoryId,
            Name = $"Order Test Product {unique}",
            Slug = $"order-test-product-{unique}",
            Sku = $"ORD-{unique.ToUpperInvariant()}",
            Description = "Created by the order tests.",
            Price = 30m,
            StockQuantity = stock
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    });
}
