using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;
using ShopSphere.Api.Features.Catalog;

namespace ShopSphere.Api.Features.Admin.Products;

[ApiController]
[Route("api/admin/products")]
[Authorize(Policy = Policies.Admin)]
public class AdminProductsController : ControllerBase
{
    private readonly AdminProductService _productService;

    public AdminProductsController(AdminProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminProductListItemDto>>> GetProducts([FromQuery] AdminProductQuery query)
    {
        return Ok(await _productService.GetProductsAsync(query));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminProductDto>> GetProduct(int id)
    {
        return Ok(await _productService.GetProductAsync(id));
    }

    [HttpPost]
    public async Task<ActionResult<AdminProductDto>> Create(CreateProductRequest request)
    {
        var product = await _productService.CreateAsync(request, User.GetUserId());
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminProductDto>> Update(int id, UpdateProductRequest request)
    {
        return Ok(await _productService.UpdateAsync(id, request, User.GetUserId()));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _productService.DeleteAsync(id, User.GetUserId());
        return NoContent();
    }

    [HttpPost("{id:int}/images")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ActionResult<ProductImageDto>> UploadImage(int id, [FromForm] UploadProductImageRequest request)
    {
        return Ok(await _productService.AddImageAsync(id, request));
    }

    [HttpPut("{id:int}/images/{imageId:int}/main")]
    public async Task<IActionResult> SetMainImage(int id, int imageId)
    {
        await _productService.SetMainImageAsync(id, imageId);
        return NoContent();
    }

    [HttpDelete("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> DeleteImage(int id, int imageId)
    {
        await _productService.DeleteImageAsync(id, imageId);
        return NoContent();
    }
}
