using System.Net;
using System.Net.Http.Json;
using ShopSphere.Api.Common;
using ShopSphere.Api.Features.Catalog;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

// These tests read the seeded catalog. Queries are scoped to seeded categories
// so products created by other tests don't change the results.
[Collection(ApiCollection.Name)]
public class CatalogTests
{
    private readonly HttpClient _client;

    public CatalogTests(ShopSphereApiFactory factory)
    {
        _client = factory.CreateApiClient();
    }

    [Fact]
    public async Task Category_filter_includes_products_from_subcategories()
    {
        var result = await SearchAsync("?category=kitchen&pageSize=48");

        Assert.Contains(result.Items, p => p.Slug == "cast-iron-skillet-12-inch");
        Assert.All(result.Items, p => Assert.Contains(p.CategorySlug, new[] { "cookware", "kitchen-storage", "tableware" }));
    }

    [Fact]
    public async Task Price_filter_uses_the_discounted_price()
    {
        // Saucepan: price 39.00, discount price 32.00. Skillet: 44.99, no discount.
        var result = await SearchAsync("?category=cookware&minPrice=30&maxPrice=35");

        Assert.Contains(result.Items, p => p.Slug == "stainless-steel-saucepan-2-qt");
        Assert.DoesNotContain(result.Items, p => p.Slug == "cast-iron-skillet-12-inch");
    }

    [Fact]
    public async Task In_stock_filter_hides_out_of_stock_products()
    {
        var all = await SearchAsync("?category=kitchen-storage");
        var inStock = await SearchAsync("?category=kitchen-storage&inStock=true");

        Assert.Contains(all.Items, p => p.Slug == "airtight-pantry-canister-set" && !p.InStock);
        Assert.DoesNotContain(inStock.Items, p => p.Slug == "airtight-pantry-canister-set");
    }

    [Fact]
    public async Task Inactive_products_are_not_visible_in_the_storefront()
    {
        var details = await _client.GetAsync("/api/products/vintage-wall-clock");
        var search = await SearchAsync("?search=clock");

        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
        Assert.DoesNotContain(search.Items, p => p.Slug == "vintage-wall-clock");
    }

    [Fact]
    public async Task Search_requires_every_word_to_match()
    {
        var match = await SearchAsync("?search=oak%20shelf");
        var noMatch = await SearchAsync("?search=oak%20sofa");

        Assert.Contains(match.Items, p => p.Slug == "oak-floating-wall-shelf");
        Assert.Empty(noMatch.Items);
    }

    [Fact]
    public async Task Pages_do_not_overlap_and_report_totals()
    {
        var page1 = await SearchAsync("?category=furniture&sort=name&pageSize=4&page=1");
        var page2 = await SearchAsync("?category=furniture&sort=name&pageSize=4&page=2");

        Assert.Equal(4, page1.Items.Count);
        Assert.Equal(page1.TotalCount, page2.TotalCount);
        Assert.Equal((int)Math.Ceiling(page1.TotalCount / 4.0), page1.TotalPages);
        Assert.Empty(page1.Items.Select(p => p.Id).Intersect(page2.Items.Select(p => p.Id)));
    }

    [Fact]
    public async Task Sort_by_price_ascending_orders_by_effective_price()
    {
        var result = await SearchAsync("?category=furniture&sort=price_asc&pageSize=48");

        var prices = result.Items.Select(p => p.EffectivePrice).ToList();
        Assert.Equal(prices.OrderBy(p => p), prices);
    }

    [Theory]
    [InlineData("?sort=cheapest")]
    [InlineData("?minPrice=100&maxPrice=50")]
    [InlineData("?pageSize=500")]
    public async Task Invalid_query_returns_400(string query)
    {
        var response = await _client.GetAsync("/api/products" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Product_details_include_category_breadcrumb()
    {
        var product = await _client.GetFromJsonAsync<ProductDetailDto>("/api/products/cast-iron-skillet-12-inch");

        Assert.Equal(new[] { "kitchen", "cookware" }, product!.Breadcrumb.Select(c => c.Slug));
    }

    private async Task<PagedResult<ProductListItemDto>> SearchAsync(string query)
    {
        var response = await _client.GetAsync("/api/products" + query);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<ProductListItemDto>>())!;
    }
}
