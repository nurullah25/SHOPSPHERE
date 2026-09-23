using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Features.Cart;
using ShopSphere.Api.Features.Wishlist;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class WishlistTests
{
    private readonly ShopSphereApiFactory _factory;

    public WishlistTests(ShopSphereApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Adding_the_same_product_twice_keeps_one_entry()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var productId = await ProductIdAsync("rattan-pendant-light");

        await client.PostAsync($"/api/wishlist/{productId}", null);
        var second = await client.PostAsync($"/api/wishlist/{productId}", null);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var items = await client.GetFromJsonAsync<List<WishlistItemDto>>("/api/wishlist");
        Assert.Single(items!, item => item.ProductId == productId);
    }

    [Fact]
    public async Task Moving_to_cart_adds_the_product_and_clears_the_wishlist_entry()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var productId = await ProductIdAsync("oak-floating-wall-shelf");
        await client.PostAsync($"/api/wishlist/{productId}", null);

        var response = await client.PostAsync($"/api/wishlist/{productId}/move-to-cart", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = (await response.Content.ReadFromJsonAsync<CartDto>())!;
        Assert.Contains(cart.Items, item => item.ProductId == productId);

        var wishlist = await client.GetFromJsonAsync<List<WishlistItemDto>>("/api/wishlist");
        Assert.DoesNotContain(wishlist!, item => item.ProductId == productId);
    }

    [Fact]
    public async Task Out_of_stock_product_stays_in_the_wishlist_when_moving_fails()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var productId = await ProductIdAsync("airtight-pantry-canister-set");
        await client.PostAsync($"/api/wishlist/{productId}", null);

        var response = await client.PostAsync($"/api/wishlist/{productId}/move-to-cart", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var wishlist = await client.GetFromJsonAsync<List<WishlistItemDto>>("/api/wishlist");
        Assert.Contains(wishlist!, item => item.ProductId == productId);
    }

    private Task<int> ProductIdAsync(string slug) =>
        _factory.WithDbAsync(db => db.Products.Where(p => p.Slug == slug).Select(p => p.Id).SingleAsync());
}
