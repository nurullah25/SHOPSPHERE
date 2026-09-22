using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Admin.Products;
using ShopSphere.Api.Features.Catalog;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class AdminProductTests
{
    // Smallest valid PNG (1x1 pixel)
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly ShopSphereApiFactory _factory;

    public AdminProductTests(ShopSphereApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Created_product_is_visible_in_storefront_and_opening_stock_is_recorded()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var request = await NewProductRequestAsync(stock: 12);
        request.Sku = request.Sku.ToLowerInvariant();

        var response = await admin.PostAsJsonAsync("/api/admin/products", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<AdminProductDto>())!;
        Assert.Equal(request.Sku.ToUpperInvariant(), created.Sku);

        var storefront = await _factory.CreateApiClient().GetFromJsonAsync<ProductDetailDto>($"/api/products/{created.Slug}");
        Assert.Equal(12, storefront!.StockQuantity);

        var movement = await _factory.WithDbAsync(db =>
            db.InventoryMovements.SingleAsync(m => m.ProductId == created.Id));
        Assert.Equal(12, movement.QuantityChange);
        Assert.Equal(InventoryChangeReason.Restock, movement.Reason);
    }

    [Fact]
    public async Task Duplicate_sku_returns_conflict()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var request = await NewProductRequestAsync();
        request.Sku = "cw-skl-012";

        var response = await admin.PostAsJsonAsync("/api/admin/products", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Discount_price_must_be_lower_than_price()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var request = await NewProductRequestAsync();
        request.DiscountPrice = request.Price;

        var response = await admin.PostAsJsonAsync("/api/admin/products", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_with_stale_row_version_returns_conflict()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(admin);

        // Two admins opened the same product. The first one saves...
        var first = ToUpdateRequest(product);
        first.Price = 95;
        var firstResponse = await admin.PutAsJsonAsync($"/api/admin/products/{product.Id}", first);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // ...then the second one saves with the version they loaded earlier
        var second = ToUpdateRequest(product);
        second.Price = 70;
        var secondResponse = await admin.PutAsJsonAsync($"/api/admin/products/{product.Id}", second);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var current = await admin.GetFromJsonAsync<AdminProductDto>($"/api/admin/products/{product.Id}");
        Assert.Equal(95, current!.Price);
    }

    [Fact]
    public async Task Price_change_is_written_to_the_audit_log()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(admin);

        var update = ToUpdateRequest(product);
        update.Price = 120;
        await admin.PutAsJsonAsync($"/api/admin/products/{product.Id}", update);

        var log = await _factory.WithDbAsync(db => db.AuditLogs
            .SingleAsync(a => a.Action == "ProductUpdated" && a.EntityId == product.Id.ToString()));
        using var details = JsonDocument.Parse(log.Details!);
        Assert.Equal(89m, details.RootElement.GetProperty("PriceBefore").GetDecimal());
        Assert.Equal(120m, details.RootElement.GetProperty("PriceAfter").GetDecimal());
    }

    [Fact]
    public async Task Product_that_was_ordered_cannot_be_deleted()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(admin);
        await AddOrderContainingAsync(product.Id);

        var response = await admin.DeleteAsync($"/api/admin/products/{product.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Product_without_orders_can_be_deleted()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(admin);

        var response = await admin.DeleteAsync($"/api/admin/products/{product.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var storefront = await _factory.CreateApiClient().GetAsync($"/api/products/{product.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, storefront.StatusCode);
    }

    [Fact]
    public async Task Uploaded_image_becomes_main_image_and_is_served()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(admin);

        var upload = await admin.PostAsync($"/api/admin/products/{product.Id}/images", ImageContent(PngBytes, "image/png"));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var image = (await upload.Content.ReadFromJsonAsync<ProductImageDto>())!;
        Assert.True(image.IsMain);

        var file = await admin.GetAsync(image.Url);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);

        var delete = await admin.DeleteAsync($"/api/admin/products/{product.Id}/images/{image.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_a_file_that_is_not_really_an_image()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(admin);
        var fakeImage = "definitely not a png"u8.ToArray();

        var response = await admin.PostAsync($"/api/admin/products/{product.Id}/images", ImageContent(fakeImage, "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<CreateProductRequest> NewProductRequestAsync(int stock = 10)
    {
        var categoryId = await _factory.WithDbAsync(db => db.Categories.Where(c => c.Slug == "cookware").Select(c => c.Id).SingleAsync());
        var unique = Guid.NewGuid().ToString("N")[..8];

        return new CreateProductRequest
        {
            Name = $"Enamel Dutch Oven {unique}",
            Sku = $"TEST-{unique}",
            Description = "Enamelled cast iron pot for braising and baking bread.",
            Price = 89,
            CategoryId = categoryId,
            StockQuantity = stock
        };
    }

    private async Task<AdminProductDto> CreateProductAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/products", await NewProductRequestAsync());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AdminProductDto>())!;
    }

    private static UpdateProductRequest ToUpdateRequest(AdminProductDto product) => new()
    {
        Name = product.Name,
        Sku = product.Sku,
        Description = product.Description,
        Price = product.Price,
        DiscountPrice = product.DiscountPrice,
        CategoryId = product.CategoryId,
        LowStockThreshold = product.LowStockThreshold,
        IsActive = product.IsActive,
        RowVersion = product.RowVersion
    };

    private static MultipartFormDataContent ImageContent(byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "File", "photo.png" } };
    }

    private Task AddOrderContainingAsync(int productId) => _factory.WithDbAsync(async db =>
    {
        var customerId = await db.Users.Where(u => u.Role == UserRole.Customer).Select(u => u.Id).FirstAsync();
        var product = await db.Products.SingleAsync(p => p.Id == productId);

        db.Orders.Add(new Order
        {
            UserId = customerId,
            Subtotal = product.Price,
            Total = product.Price,
            PlacedAt = DateTime.UtcNow,
            ShippingAddress = new OrderAddress
            {
                FullName = "Test Customer",
                Line1 = "1 Test Street",
                City = "Portland",
                PostalCode = "97205",
                Country = "United States"
            },
            Items =
            {
                new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    UnitPrice = product.Price,
                    Quantity = 1,
                    LineTotal = product.Price
                }
            }
        });

        return await db.SaveChangesAsync();
    });
}
