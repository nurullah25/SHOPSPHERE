using Microsoft.AspNetCore.Mvc;

namespace ShopSphere.Api.Features.Catalog;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly CatalogService _catalogService;

    public CategoriesController(CatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryNodeDto>>> GetTree()
    {
        return Ok(await _catalogService.GetCategoryTreeAsync());
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<CategoryDetailDto>> GetBySlug(string slug)
    {
        return Ok(await _catalogService.GetCategoryAsync(slug));
    }
}
