using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Admin.Customers;
using ShopSphere.Api.Features.Admin.Dashboard;
using ShopSphere.Api.Features.Admin.Orders;
using ShopSphere.Api.Features.Admin.Reports;
using ShopSphere.Api.Features.Cart;
using ShopSphere.Api.Features.Checkout;
using ShopSphere.Api.Features.Payments;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class ReportTests
{
    private readonly ShopSphereApiFactory _factory;

    public ReportTests(ShopSphereApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Revenue_counts_paid_orders_and_ignores_cancelled_and_unpaid_ones()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(price: 30m, stock: 20);

        var paid = await BuyAsync(await _factory.CreateCustomerClientAsync(), product.Id);
        var cancelledCustomer = await _factory.CreateCustomerClientAsync();
        var cancelled = await BuyAsync(cancelledCustomer, product.Id);
        await cancelledCustomer.PostAsJsonAsync($"/api/orders/{cancelled.OrderNumber}/cancel", new { });
        await BuyAsync(await _factory.CreateCustomerClientAsync(), product.Id, MockPaymentGateway.DeclinedToken);

        var report = await admin.GetFromJsonAsync<List<SalesByProductDto>>(
            $"/api/admin/reports/sales-by-product?top=100");

        var row = Assert.Single(report!, r => r.ProductId == product.Id);
        Assert.Equal(1, row.UnitsSold);
        // Product revenue excludes shipping, which the order total includes
        Assert.Equal(30m, row.Revenue);
        Assert.Equal(35.99m, paid.Total);
    }

    [Fact]
    public async Task Order_status_report_includes_every_status()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(price: 40m, stock: 10);
        var customer = await _factory.CreateCustomerClientAsync();
        var order = await BuyAsync(customer, product.Id);
        await customer.PostAsJsonAsync($"/api/orders/{order.OrderNumber}/cancel", new { });

        var report = await admin.GetFromJsonAsync<List<OrderStatusSummaryDto>>("/api/admin/reports/order-status");

        // Cancelled orders are missing from revenue reports but present here
        Assert.Contains(report!, row => row.Status == "Cancelled" && row.Orders >= 1);
    }

    [Fact]
    public async Task Sales_by_date_groups_orders_per_day_and_can_group_by_month()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(price: 60m, stock: 10);
        await BuyAsync(await _factory.CreateCustomerClientAsync(), product.Id);

        var daily = await admin.GetFromJsonAsync<List<SalesByPeriodDto>>("/api/admin/reports/sales-by-date?groupBy=day");
        var monthly = await admin.GetFromJsonAsync<List<SalesByPeriodDto>>("/api/admin/reports/sales-by-date?groupBy=month");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Contains(daily!, row => row.PeriodStart == today && row.Orders >= 1);
        Assert.Contains(monthly!, row => row.PeriodStart == new DateOnly(today.Year, today.Month, 1));
        Assert.True(monthly!.Count <= daily!.Count);
    }

    [Fact]
    public async Task Sales_by_category_rolls_products_up_to_their_category()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(price: 75m, stock: 10);
        await BuyAsync(await _factory.CreateCustomerClientAsync(), product.Id, quantity: 2);

        var report = await admin.GetFromJsonAsync<List<SalesByCategoryDto>>("/api/admin/reports/sales-by-category");

        var cookware = Assert.Single(report!, row => row.CategoryName == "Cookware");
        Assert.True(cookware.UnitsSold >= 2);
        Assert.True(cookware.Revenue >= 150m);
    }

    [Fact]
    public async Task Report_range_with_an_end_before_the_start_is_rejected()
    {
        var admin = await _factory.CreateAdminClientAsync();

        var response = await admin.GetAsync("/api/admin/reports/sales-by-date?from=2026-05-01&to=2026-04-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_reports_totals_pending_orders_and_low_stock()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var lowStockProduct = await CreateProductAsync(price: 20m, stock: 2);
        await BuyAsync(await _factory.CreateCustomerClientAsync(), lowStockProduct.Id, MockPaymentGateway.DeclinedToken);

        var dashboard = await admin.GetFromJsonAsync<DashboardDto>("/api/admin/dashboard?days=7");

        Assert.Equal(7, dashboard!.PeriodDays);
        Assert.Equal(7, dashboard.RevenueByDay.Count);
        Assert.True(dashboard.PendingOrders >= 1);
        Assert.Contains(dashboard.LowStock, p => p.ProductId == lowStockProduct.Id);
        Assert.True(dashboard.TotalCustomers >= 1);
        Assert.NotEmpty(dashboard.RecentOrders);
    }

    [Fact]
    public async Task Customer_list_shows_spend_from_paid_orders_only()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(price: 90m, stock: 10);
        var customer = await _factory.CreateCustomerClientAsync();
        await BuyAsync(customer, product.Id);
        var cancelled = await BuyAsync(customer, product.Id);
        await customer.PostAsJsonAsync($"/api/orders/{cancelled.OrderNumber}/cancel", new { });

        var email = (await customer.GetFromJsonAsync<ShopSphere.Api.Features.Auth.UserDto>("/api/auth/me"))!.Email;
        var page = await admin.GetFromJsonAsync<PagedResult<CustomerListItemDto>>($"/api/admin/customers?search={email}");

        var row = Assert.Single(page!.Items);
        Assert.Equal(1, row.OrderCount);
        // Free shipping above $75, and the cancelled order doesn't count
        Assert.Equal(90m, row.TotalSpent);
    }

    [Fact]
    public async Task Deactivating_a_customer_blocks_their_refresh_token()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var client = _factory.CreateApiClient();
        var (auth, refreshToken) = await AuthHelper.RegisterCustomerAsync(client);

        var response = await admin.PatchAsJsonAsync($"/api/admin/customers/{auth.User.Id}/status",
            new UpdateCustomerStatusRequest { IsActive = false });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var refreshed = await AuthHelper.RefreshAsync(client, refreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshed.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new ShopSphere.Api.Features.Auth.LoginRequest { Email = auth.User.Email, Password = AuthHelper.DefaultPassword });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Customers_cannot_read_reports_or_the_dashboard()
    {
        var customer = await _factory.CreateCustomerClientAsync();

        var dashboard = await customer.GetAsync("/api/admin/dashboard");
        var report = await customer.GetAsync("/api/admin/reports/sales-by-date");
        var customers = await customer.GetAsync("/api/admin/customers");

        Assert.Equal(HttpStatusCode.Forbidden, dashboard.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, report.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customers.StatusCode);
    }

    private static async Task<CheckoutResultDto> BuyAsync(HttpClient client, int productId, string paymentToken = MockPaymentGateway.SuccessToken, int quantity = 1)
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

    private Task<Product> CreateProductAsync(decimal price, int stock) => _factory.WithDbAsync(async db =>
    {
        var categoryId = await db.Categories.Where(c => c.Slug == "cookware").Select(c => c.Id).SingleAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var product = new Product
        {
            CategoryId = categoryId,
            Name = $"Report Test Product {unique}",
            Slug = $"report-test-product-{unique}",
            Sku = $"RPT-{unique.ToUpperInvariant()}",
            Description = "Created by the report tests.",
            Price = price,
            StockQuantity = stock
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    });
}
