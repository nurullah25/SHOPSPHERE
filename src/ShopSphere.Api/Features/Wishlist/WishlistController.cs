using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;
using ShopSphere.Api.Features.Cart;

namespace ShopSphere.Api.Features.Wishlist;

[ApiController]
[Route("api/wishlist")]
[Authorize(Policy = Policies.Customer)]
public class WishlistController : ControllerBase
{
    private readonly WishlistService _wishlistService;

    public WishlistController(WishlistService wishlistService)
    {
        _wishlistService = wishlistService;
    }

    [HttpGet]
    public async Task<ActionResult<List<WishlistItemDto>>> GetItems()
    {
        return Ok(await _wishlistService.GetItemsAsync(User.GetUserId()));
    }

    [HttpPost("{productId:int}")]
    public async Task<IActionResult> Add(int productId)
    {
        await _wishlistService.AddAsync(User.GetUserId(), productId);
        return NoContent();
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> Remove(int productId)
    {
        await _wishlistService.RemoveAsync(User.GetUserId(), productId);
        return NoContent();
    }

    [HttpPost("{productId:int}/move-to-cart")]
    public async Task<ActionResult<CartDto>> MoveToCart(int productId)
    {
        return Ok(await _wishlistService.MoveToCartAsync(User.GetUserId(), productId));
    }
}
