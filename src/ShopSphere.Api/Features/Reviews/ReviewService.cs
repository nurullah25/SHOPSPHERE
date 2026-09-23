using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Reviews;

public class ReviewService
{
    private readonly AppDbContext _db;

    public ReviewService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ProductReviewsDto> GetProductReviewsAsync(int productId, int? userId, int page, int pageSize)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId))
            throw new NotFoundException($"Product {productId} was not found.");

        var reviews = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                Rating = r.Rating,
                Title = r.Title,
                Comment = r.Comment,
                CustomerName = r.User.FirstName + " " + r.User.LastName.Substring(0, 1) + ".",
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                IsMine = userId != null && r.UserId == userId
            })
            .ToPagedResultAsync(page, pageSize);

        var counts = await _db.Reviews
            .Where(r => r.ProductId == productId)
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Rating, x => x.Count);

        var result = new ProductReviewsDto
        {
            Reviews = reviews,
            RatingCounts = Enumerable.Range(1, 5).ToDictionary(rating => rating, rating => counts.GetValueOrDefault(rating)),
            ReviewCount = counts.Values.Sum(),
            AverageRating = counts.Count == 0
                ? 0
                : Math.Round((decimal)counts.Sum(c => c.Key * c.Value) / counts.Values.Sum(), 2)
        };

        if (userId.HasValue)
        {
            var myReview = await _db.Reviews
                .AsNoTracking()
                .Where(r => r.ProductId == productId && r.UserId == userId)
                .Select(r => new ReviewDto
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Title = r.Title,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    IsMine = true
                })
                .SingleOrDefaultAsync();

            result.MyReview = myReview;
            result.CanReview = myReview == null && await HasDeliveredOrderAsync(userId.Value, productId);
        }

        return result;
    }

    public async Task<ReviewDto> CreateAsync(int userId, int productId, ReviewRequest request)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId))
            throw new NotFoundException($"Product {productId} was not found.");

        // Only customers who actually received the product can review it
        if (!await HasDeliveredOrderAsync(userId, productId))
        {
            throw new BusinessRuleException("Purchase required",
                "You can only review products from an order that has been delivered.", StatusCodes.Status403Forbidden);
        }

        if (await _db.Reviews.AnyAsync(r => r.ProductId == productId && r.UserId == userId))
            throw new BusinessRuleException("Already reviewed", "You have already reviewed this product. Edit your review instead.");

        var review = new Review
        {
            ProductId = productId,
            UserId = userId,
            Rating = request.Rating,
            Title = request.Title?.Trim(),
            Comment = request.Comment?.Trim()
        };

        await using var transaction = await _db.Database.BeginTransactionAsync();

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();
        await UpdateProductRatingAsync(productId);

        await transaction.CommitAsync();

        return ToDto(review);
    }

    public async Task<ReviewDto> UpdateAsync(int userId, int reviewId, ReviewRequest request)
    {
        // Filtering by user id means someone else's review simply isn't found
        var review = await _db.Reviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId)
            ?? throw new NotFoundException("Review not found.");

        review.Rating = request.Rating;
        review.Title = request.Title?.Trim();
        review.Comment = request.Comment?.Trim();

        await using var transaction = await _db.Database.BeginTransactionAsync();

        await _db.SaveChangesAsync();
        await UpdateProductRatingAsync(review.ProductId);

        await transaction.CommitAsync();

        return ToDto(review);
    }

    public async Task DeleteAsync(int userId, int reviewId)
    {
        var review = await _db.Reviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId)
            ?? throw new NotFoundException("Review not found.");

        await using var transaction = await _db.Database.BeginTransactionAsync();

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync();
        await UpdateProductRatingAsync(review.ProductId);

        await transaction.CommitAsync();
    }

    private Task<bool> HasDeliveredOrderAsync(int userId, int productId) =>
        _db.OrderItems.AnyAsync(item =>
            item.ProductId == productId &&
            item.Order.UserId == userId &&
            item.Order.Status == OrderStatus.Delivered);

    // The product keeps a copy of the rating so listings don't have to
    // aggregate reviews on every query
    private async Task UpdateProductRatingAsync(int productId)
    {
        var stats = await _db.Reviews
            .Where(r => r.ProductId == productId)
            .GroupBy(r => r.ProductId)
            .Select(g => new { Count = g.Count(), Average = g.Average(r => (double)r.Rating) })
            .SingleOrDefaultAsync();

        var count = stats?.Count ?? 0;
        var average = stats == null ? 0m : Math.Round((decimal)stats.Average, 2);

        await _db.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.ReviewCount, count)
                .SetProperty(p => p.AverageRating, average));
    }

    private static ReviewDto ToDto(Review review) => new()
    {
        Id = review.Id,
        Rating = review.Rating,
        Title = review.Title,
        Comment = review.Comment,
        CreatedAt = review.CreatedAt,
        UpdatedAt = review.UpdatedAt,
        IsMine = true
    };
}
