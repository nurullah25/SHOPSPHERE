using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;

namespace ShopSphere.Api.Features.Reviews;

[ApiController]
public class ReviewsController : ControllerBase
{
    private readonly ReviewService _reviewService;

    public ReviewsController(ReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    // Anyone can read reviews. When a customer is signed in, the response also
    // says whether they can write one.
    [HttpGet("api/products/{productId:int}/reviews")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductReviewsDto>> GetReviews(
        int productId,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 50)] int pageSize = 5)
    {
        var userId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : (int?)null;
        return Ok(await _reviewService.GetProductReviewsAsync(productId, userId, page, pageSize));
    }

    [HttpPost("api/products/{productId:int}/reviews")]
    [Authorize(Policy = Policies.Customer)]
    public async Task<ActionResult<ReviewDto>> Create(int productId, ReviewRequest request)
    {
        return Ok(await _reviewService.CreateAsync(User.GetUserId(), productId, request));
    }

    [HttpPut("api/reviews/{id:int}")]
    [Authorize(Policy = Policies.Customer)]
    public async Task<ActionResult<ReviewDto>> Update(int id, ReviewRequest request)
    {
        return Ok(await _reviewService.UpdateAsync(User.GetUserId(), id, request));
    }

    [HttpDelete("api/reviews/{id:int}")]
    [Authorize(Policy = Policies.Customer)]
    public async Task<IActionResult> Delete(int id)
    {
        await _reviewService.DeleteAsync(User.GetUserId(), id);
        return NoContent();
    }
}
