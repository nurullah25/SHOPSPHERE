using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Features.Admin.Categories;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class AdminCategoryTests
{
    private readonly ShopSphereApiFactory _factory;

    public AdminCategoryTests(ShopSphereApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Category_with_products_cannot_be_deleted()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var cookwareId = await CategoryIdAsync("cookware");

        var response = await admin.DeleteAsync($"/api/admin/categories/{cookwareId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Category_cannot_be_moved_under_its_own_subcategory()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var kitchenId = await CategoryIdAsync("kitchen");
        var cookwareId = await CategoryIdAsync("cookware");

        var response = await admin.PutAsJsonAsync($"/api/admin/categories/{kitchenId}", new CategoryRequest
        {
            Name = "Kitchen",
            ParentId = cookwareId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var parentId = await _factory.WithDbAsync(db => db.Categories.Where(c => c.Id == kitchenId).Select(c => c.ParentId).SingleAsync());
        Assert.Null(parentId);
    }

    [Fact]
    public async Task Empty_subcategory_can_be_created_and_deleted()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var kitchenId = await CategoryIdAsync("kitchen");

        var create = await admin.PostAsJsonAsync("/api/admin/categories", new CategoryRequest
        {
            Name = $"Knives {Guid.NewGuid():N}"[..14],
            ParentId = kitchenId
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var category = (await create.Content.ReadFromJsonAsync<AdminCategoryDto>())!;
        Assert.StartsWith("knives-", category.Slug);

        var delete = await admin.DeleteAsync($"/api/admin/categories/{category.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    private Task<int> CategoryIdAsync(string slug) =>
        _factory.WithDbAsync(db => db.Categories.Where(c => c.Slug == slug).Select(c => c.Id).SingleAsync());
}
