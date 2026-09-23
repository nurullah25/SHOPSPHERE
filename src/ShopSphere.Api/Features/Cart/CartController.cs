using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;

namespace ShopSphere.Api.Features.Cart;

[ApiController]
[Route("api/cart")]
[Authorize(Policy = Policies.Customer)]
public class CartController : ControllerBase
{
    private readonly CartService _cartService;

    public CartController(CartService cartService)
    {
        _cartService = cartService;
    }

    [HttpGet]
    public async Task<ActionResult<CartDto>> GetCart()
    {
        return Ok(await _cartService.GetCartAsync(User.GetUserId()));
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem(AddToCartRequest request)
    {
        return Ok(await _cartService.AddItemAsync(User.GetUserId(), request));
    }

    [HttpPut("items/{productId:int}")]
    public async Task<ActionResult<CartDto>> UpdateItem(int productId, UpdateCartItemRequest request)
    {
        return Ok(await _cartService.UpdateQuantityAsync(User.GetUserId(), productId, request.Quantity));
    }

    [HttpDelete("items/{productId:int}")]
    public async Task<ActionResult<CartDto>> RemoveItem(int productId)
    {
        return Ok(await _cartService.RemoveItemAsync(User.GetUserId(), productId));
    }

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        await _cartService.ClearAsync(User.GetUserId());
        return NoContent();
    }
}
