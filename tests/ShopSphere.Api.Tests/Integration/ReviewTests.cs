using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Admin.Orders;
using ShopSphere.Api.Features.Cart;
using ShopSphere.Api.Features.Checkout;
using ShopSphere.Api.Features.Payments;
using ShopSphere.Api.Features.Reviews;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class ReviewTests
{
    private readonly ShopSphereApiFactory _factory;

    public ReviewTests(ShopSphereApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Customer_who_never_bought_the_product_cannot_review_it()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync();

        var response = await client.PostAsJsonAsync($"/api/products/{product.Id}/reviews", new ReviewRequest { Rating = 5 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Buying_is_not_enough_the_order_has_to_be_delivered()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var product = await CreateProductAsync();
        var order = await BuyAsync(client, product.Id);

        var beforeDelivery = await client.PostAsJsonAsync($"/api/products/{product.Id}/reviews", new ReviewRequest { Rating = 4 });
        Assert.Equal(HttpStatusCode.Forbidden, beforeDelivery.StatusCode);

        await DeliverAsync(admin, order.OrderNumber);

        var afterDelivery = await client.PostAsJsonAsync($"/api/products/{product.Id}/reviews",
            new ReviewRequest { Rating = 4, Title = "Solid pan", Comment = "Heats evenly and cleans up easily." });
        Assert.Equal(HttpStatusCode.OK, afterDelivery.StatusCode);
    }

    [Fact]
    public async Task Review_updates_the_rating_shown_on_the_product()
    {
        var product = await CreateProductAsync();
        var admin = await _factory.CreateAdminClientAsync();

        await ReviewAsync(product.Id, admin, rating: 5);
        await ReviewAsync(product.Id, admin, rating: 4);

        var stats = await _factory.WithDbAsync(db => db.Products
            .Where(p => p.Id == product.Id)
            .Select(p => new { p.AverageRating, p.ReviewCount })
            .SingleAsync());

        Assert.Equal(2, stats.ReviewCount);
        Assert.Equal(4.5m, stats.AverageRating);
    }

    [Fact]
    public async Task Second_review_for_the_same_product_is_rejected()
    {
        var product = await CreateProductAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await ReviewAsync(product.Id, admin, rating: 5);

        var response = await customer.PostAsJsonAsync($"/api/products/{product.Id}/reviews", new ReviewRequest { Rating = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Customer_can_edit_and_delete_their_own_review()
    {
        var product = await CreateProductAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await ReviewAsync(product.Id, admin, rating: 2);

        var reviews = await customer.GetFromJsonAsync<ProductReviewsDto>($"/api/products/{product.Id}/reviews");
        var reviewId = reviews!.MyReview!.Id;

        var updated = await customer.PutAsJsonAsync($"/api/reviews/{reviewId}",
            new ReviewRequest { Rating = 5, Comment = "Changed my mind, it's great." });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(5m, await AverageRatingAsync(product.Id));

        var deleted = await customer.DeleteAsync($"/api/reviews/{reviewId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var stats = await _factory.WithDbAsync(db => db.Products
            .Where(p => p.Id == product.Id)
            .Select(p => new { p.AverageRating, p.ReviewCount })
            .SingleAsync());
        Assert.Equal(0, stats.ReviewCount);
        Assert.Equal(0m, stats.AverageRating);
    }

    [Fact]
    public async Task Customers_cannot_touch_someone_elses_review()
    {
        var product = await CreateProductAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var owner = await ReviewAsync(product.Id, admin, rating: 5);
        var reviews = await owner.GetFromJsonAsync<ProductReviewsDto>($"/api/products/{product.Id}/reviews");
        var reviewId = reviews!.MyReview!.Id;

        var other = await _factory.CreateCustomerClientAsync();
        var edit = await other.PutAsJsonAsync($"/api/reviews/{reviewId}", new ReviewRequest { Rating = 1 });
        var delete = await other.DeleteAsync($"/api/reviews/{reviewId}");

        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(5m, await AverageRatingAsync(product.Id));
    }

    [Fact]
    public async Task Reviews_are_public_and_show_shortened_customer_names()
    {
        var product = await CreateProductAsync();
        var admin = await _factory.CreateAdminClientAsync();
        await ReviewAsync(product.Id, admin, rating: 5, comment: "Would buy again.");

        var anonymous = _factory.CreateApiClient();
        var reviews = await anonymous.GetFromJsonAsync<ProductReviewsDto>($"/api/products/{product.Id}/reviews");

        var review = Assert.Single(reviews!.Reviews.Items);
        Assert.Equal("Test C.", review.CustomerName);
        Assert.False(review.IsMine);
        Assert.False(reviews.CanReview);
        Assert.Equal(1, reviews.RatingCounts[5]);
        Assert.Equal(0, reviews.RatingCounts[1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Rating_must_be_between_one_and_five(int rating)
    {
        var client = await _factory.CreateCustomerClientAsync();
        var product = await CreateProductAsync();

        var response = await client.PostAsJsonAsync($"/api/products/{product.Id}/reviews", new ReviewRequest { Rating = rating });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Creates a customer who bought and received the product, then reviews it
    private async Task<HttpClient> ReviewAsync(int productId, HttpClient admin, int rating, string? comment = null)
    {
        var customer = await _factory.CreateCustomerClientAsync();
        var order = await BuyAsync(customer, productId);
        await DeliverAsync(admin, order.OrderNumber);

        var response = await customer.PostAsJsonAsync($"/api/products/{productId}/reviews",
            new ReviewRequest { Rating = rating, Comment = comment });
        response.EnsureSuccessStatusCode();

        return customer;
    }

    private async Task DeliverAsync(HttpClient admin, string orderNumber)
    {
        var orderId = await _factory.WithDbAsync(db => db.Orders
            .Where(o => o.OrderNumber == orderNumber)
            .Select(o => o.Id)
            .SingleAsync());

        foreach (var status in new[] { OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered })
        {
            var response = await admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/status",
                new ChangeOrderStatusRequest { Status = status });
            response.EnsureSuccessStatusCode();
        }
    }

    private static async Task<CheckoutResultDto> BuyAsync(HttpClient client, int productId)
    {
        var add = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = productId, Quantity = 1 });
        add.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/checkout", new PlaceOrderRequest
        {
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

    private Task<decimal> AverageRatingAsync(int productId) =>
        _factory.WithDbAsync(db => db.Products.Where(p => p.Id == productId).Select(p => p.AverageRating).SingleAsync());

    private Task<Product> CreateProductAsync() => _factory.WithDbAsync(async db =>
    {
        var categoryId = await db.Categories.Where(c => c.Slug == "cookware").Select(c => c.Id).SingleAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var product = new Product
        {
            CategoryId = categoryId,
            Name = $"Review Test Product {unique}",
            Slug = $"review-test-product-{unique}",
            Sku = $"REV-{unique.ToUpperInvariant()}",
            Description = "Created by the review tests.",
            Price = 30m,
            StockQuantity = 25
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    });
}
