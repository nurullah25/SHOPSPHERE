using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Features.Cart;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class CartTests
{
    private readonly ShopSphereApiFactory _factory;

    public CartTests(ShopSphereApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cart_requires_a_signed_in_customer()
    {
        var anonymous = await _factory.CreateApiClient().GetAsync("/api/cart");
        var admin = await (await _factory.CreateAdminClientAsync()).GetAsync("/api/cart");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
    }

    [Fact]
    public async Task Adding_the_same_product_twice_increases_the_quantity()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var productId = await ProductIdAsync("cast-iron-skillet-12-inch");

        await AddAsync(client, productId, 2);
        var cart = await AddAsync(client, productId, 3);

        var item = Assert.Single(cart.Items);
        Assert.Equal(5, item.Quantity);
        Assert.Equal(item.UnitPrice * 5, item.LineTotal);
        Assert.Equal(5, cart.ItemCount);
    }

    [Fact]
    public async Task Cannot_add_more_than_the_available_stock()
    {
        var client = await _factory.CreateCustomerClientAsync();
        // Seeded with 2 in stock
        var productId = await ProductIdAsync("cotton-percale-sheet-set-queen");

        var response = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = productId, Quantity = 3 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var cart = await client.GetFromJsonAsync<CartDto>("/api/cart");
        Assert.Empty(cart!.Items);
    }

    [Fact]
    public async Task Stock_limit_also_applies_to_the_total_quantity_in_the_cart()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var productId = await ProductIdAsync("cotton-percale-sheet-set-queen");

        await AddAsync(client, productId, 2);
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = productId, Quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Out_of_stock_and_inactive_products_cannot_be_added()
    {
        var client = await _factory.CreateCustomerClientAsync();

        var outOfStock = await client.PostAsJsonAsync("/api/cart/items",
            new AddToCartRequest { ProductId = await ProductIdAsync("airtight-pantry-canister-set"), Quantity = 1 });
        var inactive = await client.PostAsJsonAsync("/api/cart/items",
            new AddToCartRequest { ProductId = await ProductIdAsync("vintage-wall-clock"), Quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, outOfStock.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
    }

    [Fact]
    public async Task Cart_uses_the_current_price_from_the_database()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var productId = await ProductIdAsync("bamboo-spice-rack");

        var before = await AddAsync(client, productId, 1);

        await _factory.WithDbAsync(db => db.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, p => p.Price + 5)));

        var after = await client.GetFromJsonAsync<CartDto>("/api/cart");

        Assert.Equal(before.Items[0].UnitPrice + 5, after!.Items[0].UnitPrice);
        Assert.Equal(after.Items[0].UnitPrice, after.Subtotal);

        // Leave the seeded price as it was for other tests
        await _factory.WithDbAsync(db => db.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, p => p.Price - 5)));
    }

    [Fact]
    public async Task Item_is_flagged_when_stock_drops_below_the_cart_quantity()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var productId = await ProductIdAsync("acacia-wood-serving-board");
        await AddAsync(client, productId, 4);

        await _factory.WithDbAsync(db => db.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 1)));

        var cart = await client.GetFromJsonAsync<CartDto>("/api/cart");

        Assert.True(cart!.HasUnavailableItems);
        Assert.Equal("Only 1 left in stock.", cart.Items[0].Issue);
        Assert.Equal(0, cart.Subtotal);

        await _factory.WithDbAsync(db => db.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 18)));
    }

    [Fact]
    public async Task Quantity_can_be_changed_and_items_removed()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var productId = await ProductIdAsync("ceramic-table-lamp");
        await AddAsync(client, productId, 2);

        var updated = await client.PutAsJsonAsync($"/api/cart/items/{productId}", new UpdateCartItemRequest { Quantity = 4 });
        var cart = (await updated.Content.ReadFromJsonAsync<CartDto>())!;
        Assert.Equal(4, cart.Items[0].Quantity);

        var removed = await client.DeleteAsync($"/api/cart/items/{productId}");
        var emptied = (await removed.Content.ReadFromJsonAsync<CartDto>())!;
        Assert.Empty(emptied.Items);
    }

    [Fact]
    public async Task Quantity_of_zero_is_rejected_by_validation()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var productId = await ProductIdAsync("ceramic-table-lamp");
        await AddAsync(client, productId, 1);

        var response = await client.PutAsJsonAsync($"/api/cart/items/{productId}", new UpdateCartItemRequest { Quantity = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<CartDto> AddAsync(HttpClient client, int productId, int quantity)
    {
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = productId, Quantity = quantity });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CartDto>())!;
    }

    private Task<int> ProductIdAsync(string slug) =>
        _factory.WithDbAsync(db => db.Products.Where(p => p.Slug == slug).Select(p => p.Id).SingleAsync());
}
