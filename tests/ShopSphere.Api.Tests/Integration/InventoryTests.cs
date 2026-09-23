using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Admin.Inventory;
using ShopSphere.Api.Features.Cart;
using ShopSphere.Api.Features.Checkout;
using ShopSphere.Api.Features.Orders;
using ShopSphere.Api.Features.Payments;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class InventoryTests
{
    private readonly ShopSphereApiFactory _factory;

    public InventoryTests(ShopSphereApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Restock_increases_stock_and_records_a_movement_and_audit_entry()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(stock: 4);

        var response = await admin.PostAsJsonAsync($"/api/admin/inventory/{product.Id}/adjustments", new StockAdjustmentRequest
        {
            QuantityChange = 20,
            Reason = InventoryChangeReason.Restock,
            Note = "Delivery from supplier"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var movement = (await response.Content.ReadFromJsonAsync<InventoryMovementDto>())!;
        Assert.Equal(20, movement.QuantityChange);
        Assert.Equal(24, movement.QuantityAfter);

        Assert.Equal(24, await StockAsync(product.Id));

        var log = await _factory.WithDbAsync(db => db.AuditLogs
            .SingleAsync(a => a.Action == "StockAdjusted" && a.EntityId == product.Id.ToString()));
        Assert.Contains("Restock", log.Details);
    }

    [Fact]
    public async Task Adjustment_cannot_push_stock_below_zero()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(stock: 3);

        var response = await admin.PostAsJsonAsync($"/api/admin/inventory/{product.Id}/adjustments", new StockAdjustmentRequest
        {
            QuantityChange = -5,
            Reason = InventoryChangeReason.Adjustment,
            Note = "Damaged in the warehouse"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(3, await StockAsync(product.Id));
        var movements = await _factory.WithDbAsync(db => db.InventoryMovements.CountAsync(m => m.ProductId == product.Id));
        Assert.Equal(0, movements);
    }

    [Fact]
    public async Task Sale_reasons_cannot_be_used_for_manual_adjustments()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(stock: 5);

        var response = await admin.PostAsJsonAsync($"/api/admin/inventory/{product.Id}/adjustments", new StockAdjustmentRequest
        {
            QuantityChange = -1,
            Reason = InventoryChangeReason.Sale
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Low_stock_filter_returns_products_at_or_below_their_threshold()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var low = await CreateProductAsync(stock: 2);
        var healthy = await CreateProductAsync(stock: 40);

        var page = await admin.GetFromJsonAsync<PagedResult<InventoryItemDto>>("/api/admin/inventory?lowStock=true&pageSize=100");

        Assert.Contains(page!.Items, item => item.ProductId == low.Id && item.IsLowStock);
        Assert.DoesNotContain(page.Items, item => item.ProductId == healthy.Id);
    }

    [Fact]
    public async Task Movements_show_the_whole_history_of_a_product()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 10);

        await admin.PostAsJsonAsync($"/api/admin/inventory/{product.Id}/adjustments", new StockAdjustmentRequest
        {
            QuantityChange = 5,
            Reason = InventoryChangeReason.Restock
        });
        var order = await BuyAsync(customer, product.Id, quantity: 2);

        var page = await admin.GetFromJsonAsync<PagedResult<InventoryMovementDto>>($"/api/admin/inventory/{product.Id}/movements");

        // Newest first: the sale, then the restock
        Assert.Equal("Sale", page!.Items[0].Reason);
        Assert.Equal(-2, page.Items[0].QuantityChange);
        Assert.Equal(order.OrderNumber, page.Items[0].OrderNumber);
        Assert.Equal("Restock", page.Items[1].Reason);
        Assert.Equal(15, page.Items[1].QuantityAfter);
    }

    [Fact]
    public async Task Inventory_list_shows_stock_reserved_by_unpaid_orders()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 9);

        // A declined payment leaves the order pending, holding its stock
        await BuyAsync(customer, product.Id, quantity: 3, paymentToken: MockPaymentGateway.DeclinedToken);

        var page = await admin.GetFromJsonAsync<PagedResult<InventoryItemDto>>($"/api/admin/inventory?search={product.Sku}");

        var item = Assert.Single(page!.Items);
        Assert.Equal(6, item.StockQuantity);
        Assert.Equal(3, item.ReservedForPendingOrders);
    }

    [Fact]
    public async Task Unpaid_orders_are_cancelled_after_the_timeout_and_their_stock_comes_back()
    {
        var customer = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 7);
        var abandoned = await BuyAsync(customer, product.Id, quantity: 2, paymentToken: MockPaymentGateway.DeclinedToken);
        var recent = await BuyAsync(customer, product.Id, quantity: 1, paymentToken: MockPaymentGateway.DeclinedToken);
        var paid = await BuyAsync(customer, product.Id, quantity: 1);

        Assert.Equal(3, await StockAsync(product.Id));

        // Pretend the first order was placed an hour ago
        await _factory.WithDbAsync(db => db.Orders
            .Where(o => o.OrderNumber == abandoned.OrderNumber)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.PlacedAt, DateTime.UtcNow.AddHours(-1))));

        using var scope = _factory.Services.CreateScope();
        var expiry = scope.ServiceProvider.GetRequiredService<PendingOrderExpiry>();
        var expired = await expiry.ExpireAsync();

        Assert.Equal(1, expired);
        Assert.Equal(5, await StockAsync(product.Id));

        var statuses = await _factory.WithDbAsync(db => db.Orders
            .Where(o => o.OrderNumber == abandoned.OrderNumber || o.OrderNumber == recent.OrderNumber || o.OrderNumber == paid.OrderNumber)
            .ToDictionaryAsync(o => o.OrderNumber, o => o.Status));

        Assert.Equal(OrderStatus.Cancelled, statuses[abandoned.OrderNumber]);
        Assert.Equal(OrderStatus.Pending, statuses[recent.OrderNumber]);
        Assert.Equal(OrderStatus.Confirmed, statuses[paid.OrderNumber]);

        var movement = await _factory.WithDbAsync(db => db.InventoryMovements
            .SingleAsync(m => m.ProductId == product.Id && m.Reason == InventoryChangeReason.OrderExpired));
        Assert.Equal(2, movement.QuantityChange);
    }

    [Fact]
    public async Task Customers_cannot_adjust_stock()
    {
        var customer = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(stock: 5);

        var response = await customer.PostAsJsonAsync($"/api/admin/inventory/{product.Id}/adjustments", new StockAdjustmentRequest
        {
            QuantityChange = 100,
            Reason = InventoryChangeReason.Restock
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<CheckoutResultDto> BuyAsync(HttpClient client, int productId, int quantity = 1, string paymentToken = MockPaymentGateway.SuccessToken)
    {
        var add = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = productId, Quantity = quantity });
        add.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/checkout", new PlaceOrderRequest
        {
            PaymentToken = paymentToken,
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

    private Task<Product> CreateProductAsync(int stock) => _factory.WithDbAsync(async db =>
    {
        var categoryId = await db.Categories.Where(c => c.Slug == "cookware").Select(c => c.Id).SingleAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var product = new Product
        {
            CategoryId = categoryId,
            Name = $"Inventory Test Product {unique}",
            Slug = $"inventory-test-product-{unique}",
            Sku = $"INV-{unique.ToUpperInvariant()}",
            Description = "Created by the inventory tests.",
            Price = 25m,
            StockQuantity = stock
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    });
}
