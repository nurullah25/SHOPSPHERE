using System.ComponentModel.DataAnnotations;
using ShopSphere.Api.Common;

namespace ShopSphere.Api.Features.Reviews;

public class ReviewDto
{
    public int Id { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Comment { get; set; }

    // Shortened for privacy, e.g. "Sarah M."
    public string CustomerName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsMine { get; set; }
}

public class ProductReviewsDto
{
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }

    // Rating (1-5) to number of reviews
    public Dictionary<int, int> RatingCounts { get; set; } = new();

    public PagedResult<ReviewDto> Reviews { get; set; } = new();

    // True when the signed-in customer has a delivered order with this product
    // and hasn't reviewed it yet
    public bool CanReview { get; set; }
    public ReviewDto? MyReview { get; set; }
}

public class ReviewRequest
{
    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(150)]
    public string? Title { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }
}
