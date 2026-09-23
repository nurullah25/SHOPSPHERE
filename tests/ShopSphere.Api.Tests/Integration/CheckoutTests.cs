using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Cart;
using ShopSphere.Api.Features.Checkout;
using ShopSphere.Api.Features.Orders;
using ShopSphere.Api.Features.Payments;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class CheckoutTests
{
    private readonly ShopSphereApiFactory _factory;

    public CheckoutTests(ShopSphereApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Successful_checkout_creates_the_order_takes_stock_and_empties_the_cart()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(price: 20m, stock: 10);
        await AddToCartAsync(client, product.Id, 3);

        var result = await PlaceOrderAsync(client);

        Assert.Equal("Confirmed", result.Status);
        Assert.Equal("Succeeded", result.PaymentStatus);
        Assert.Equal(65.99m, result.Total); // 60 subtotal + 5.99 shipping, below the free shipping threshold

        var stock = await _factory.WithDbAsync(db => db.Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync());
        Assert.Equal(7, stock);

        var cart = await client.GetFromJsonAsync<CartDto>("/api/cart");
        Assert.Empty(cart!.Items);

        var movement = await _factory.WithDbAsync(db => db.InventoryMovements
            .SingleAsync(m => m.ProductId == product.Id && m.Reason == InventoryChangeReason.Sale));
        Assert.Equal(-3, movement.QuantityChange);
        Assert.Equal(7, movement.QuantityAfter);

        var order = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{result.OrderNumber}");
        Assert.Equal(2, order!.StatusHistory.Count);
        Assert.Equal("Confirmed", order.StatusHistory[^1].ToStatus);
    }

    [Fact]
    public async Task Order_keeps_the_price_that_was_paid_when_the_product_price_changes_later()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(price: 50m, stock: 5);
        await AddToCartAsync(client, product.Id, 2);
        var result = await PlaceOrderAsync(client);

        await _factory.WithDbAsync(db => db.Products
            .Where(p => p.Id == product.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, 999m)));

        var order = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{result.OrderNumber}");

        Assert.Equal(50m, order!.Items[0].UnitPrice);
        Assert.Equal(100m, order.Subtotal);
    }

    [Fact]
    public async Task Free_shipping_applies_above_the_threshold_and_percentage_coupon_is_capped_by_the_subtotal()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(price: 44.99m, stock: 10);
        await AddToCartAsync(client, product.Id, 2);

        // WELCOME10: 10% off, minimum $50, max discount $100
        var summary = await SummaryAsync(client, "welcome10");

        Assert.Equal(89.98m, summary.Subtotal);
        Assert.Equal(9.00m, summary.DiscountAmount);
        Assert.Equal(0m, summary.ShippingCost);
        Assert.Equal(80.98m, summary.Total);
    }

    [Fact]
    public async Task Expired_and_unknown_coupons_are_rejected_and_no_order_is_created()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(price: 60m, stock: 5);
        await AddToCartAsync(client, product.Id, 1);

        var expired = await client.PostAsJsonAsync("/api/checkout", NewOrder("SUMMER25"));
        var unknown = await client.PostAsJsonAsync("/api/checkout", NewOrder("NOPE123"));

        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        var orders = await _factory.WithDbAsync(db => db.Orders.CountAsync(o => o.Items.Any(i => i.ProductId == product.Id)));
        Assert.Equal(0, orders);

        // The cart and the stock are untouched
        var stock = await _factory.WithDbAsync(db => db.Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync());
        Assert.Equal(5, stock);
    }

    [Fact]
    public async Task Coupon_below_its_minimum_order_amount_is_rejected()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(price: 20m, stock: 5);
        await AddToCartAsync(client, product.Id, 1);

        // WELCOME10 needs at least $50
        var response = await client.PostAsJsonAsync("/api/checkout/summary", new CheckoutSummaryRequest { CouponCode = "WELCOME10" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Coupon_with_a_per_customer_limit_cannot_be_used_twice()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(price: 80m, stock: 10);

        await AddToCartAsync(client, product.Id, 1);
        await PlaceOrderAsync(client, "WELCOME10");

        await AddToCartAsync(client, product.Id, 1);
        var second = await client.PostAsJsonAsync("/api/checkout", NewOrder("WELCOME10"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Coupon_usage_limit_is_shared_across_customers()
    {
        var code = $"ONCE{Guid.NewGuid():N}"[..12];
        await _factory.WithDbAsync(async db =>
        {
            db.Coupons.Add(new Coupon
            {
                Code = code.ToUpperInvariant(),
                DiscountType = DiscountType.FixedAmount,
                DiscountValue = 5,
                UsageLimit = 1
            });
            return await db.SaveChangesAsync();
        });

        var product = await CreateProductAsync(price: 30m, stock: 10);

        var first = await _factory.CreateCustomerClientAsync();
        await AddToCartAsync(first, product.Id, 1);
        await PlaceOrderAsync(first, code);

        var second = await _factory.CreateCustomerClientAsync();
        await AddToCartAsync(second, product.Id, 1);
        var response = await second.PostAsJsonAsync("/api/checkout", NewOrder(code));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_fails_when_stock_dropped_after_the_product_was_added_to_the_cart()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(price: 25m, stock: 5);
        await AddToCartAsync(client, product.Id, 4);

        await _factory.WithDbAsync(db => db.Products
            .Where(p => p.Id == product.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 2)));

        var response = await client.PostAsJsonAsync("/api/checkout", NewOrder());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Nothing was taken and the cart is still there to fix
        var stock = await _factory.WithDbAsync(db => db.Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync());
        Assert.Equal(2, stock);
        var cart = await client.GetFromJsonAsync<CartDto>("/api/cart");
        Assert.Single(cart!.Items);
    }

    [Fact]
    public async Task Two_customers_buying_the_last_unit_at_the_same_time_produce_one_order()
    {
        var product = await CreateProductAsync(price: 99m, stock: 1);

        var first = await _factory.CreateCustomerClientAsync();
        var second = await _factory.CreateCustomerClientAsync();
        await AddToCartAsync(first, product.Id, 1);
        await AddToCartAsync(second, product.Id, 1);

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/checkout", NewOrder()),
            second.PostAsJsonAsync("/api/checkout", NewOrder()));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var stock = await _factory.WithDbAsync(db => db.Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync());
        Assert.Equal(0, stock);

        var orders = await _factory.WithDbAsync(db => db.Orders.CountAsync(o => o.Items.Any(i => i.ProductId == product.Id)));
        Assert.Equal(1, orders);
    }

    [Fact]
    public async Task Declined_payment_leaves_the_order_pending_and_a_retry_can_confirm_it()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(price: 35m, stock: 4);
        await AddToCartAsync(client, product.Id, 1);

        var declined = await PlaceOrderAsync(client, paymentToken: MockPaymentGateway.DeclinedToken);

        Assert.False(declined.PaymentSucceeded);
        Assert.Equal("Pending", declined.Status);
        Assert.Equal("Failed", declined.PaymentStatus);

        // Stock is held for the pending order
        var stock = await _factory.WithDbAsync(db => db.Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync());
        Assert.Equal(3, stock);

        var retry = await client.PostAsJsonAsync($"/api/orders/{declined.OrderNumber}/payments",
            new RetryPaymentRequest { PaymentToken = MockPaymentGateway.SuccessToken });
        var retried = (await retry.Content.ReadFromJsonAsync<CheckoutResultDto>())!;

        Assert.Equal("Confirmed", retried.Status);

        var order = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{declined.OrderNumber}");
        Assert.Equal("Succeeded", order!.PaymentStatus);
    }

    [Fact]
    public async Task Checkout_with_an_empty_cart_is_rejected()
    {
        var client = await _factory.CreateCustomerClientAsync();

        var response = await client.PostAsJsonAsync("/api/checkout", NewOrder());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customers_cannot_read_another_customers_order()
    {
        var buyer = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync(price: 45m, stock: 3);
        await AddToCartAsync(buyer, product.Id, 1);
        var result = await PlaceOrderAsync(buyer);

        var other = await _factory.CreateCustomerClientAsync();
        var response = await other.GetAsync($"/api/orders/{result.OrderNumber}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static PlaceOrderRequest NewOrder(string? couponCode = null, string paymentToken = MockPaymentGateway.SuccessToken) => new()
    {
        CouponCode = couponCode,
        PaymentToken = paymentToken,
        ShippingAddress = new ShippingAddressRequest
        {
            FullName = "Test Customer",
            Line1 = "742 Maple Avenue",
            City = "Portland",
            State = "OR",
            PostalCode = "97205",
            Country = "United States"
        }
    };

    private static async Task<CheckoutResultDto> PlaceOrderAsync(HttpClient client, string? couponCode = null, string paymentToken = MockPaymentGateway.SuccessToken)
    {
        var response = await client.PostAsJsonAsync("/api/checkout", NewOrder(couponCode, paymentToken));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CheckoutResultDto>())!;
    }

    private static async Task<CheckoutSummaryDto> SummaryAsync(HttpClient client, string? couponCode)
    {
        var response = await client.PostAsJsonAsync("/api/checkout/summary", new CheckoutSummaryRequest { CouponCode = couponCode });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CheckoutSummaryDto>())!;
    }

    private static async Task AddToCartAsync(HttpClient client, int productId, int quantity)
    {
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = productId, Quantity = quantity });
        response.EnsureSuccessStatusCode();
    }

    // Each test gets its own product so stock changes can't affect other tests
    private Task<Product> CreateProductAsync(decimal price, int stock) => _factory.WithDbAsync(async db =>
    {
        var categoryId = await db.Categories.Where(c => c.Slug == "cookware").Select(c => c.Id).SingleAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var product = new Product
        {
            CategoryId = categoryId,
            Name = $"Checkout Test Product {unique}",
            Slug = $"checkout-test-product-{unique}",
            Sku = $"CHK-{unique.ToUpperInvariant()}",
            Description = "Created by the checkout tests.",
            Price = price,
            StockQuantity = stock
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    });
}
