using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;

namespace ShopSphere.Api.Features.Catalog;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly CatalogService _catalogService;

    public ProductsController(CatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> Search([FromQuery] ProductQuery query)
    {
        return Ok(await _catalogService.SearchProductsAsync(query));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ProductDetailDto>> GetBySlug(string slug)
    {
        return Ok(await _catalogService.GetProductAsync(slug));
    }
}
