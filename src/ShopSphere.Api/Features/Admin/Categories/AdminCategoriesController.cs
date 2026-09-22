using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;

namespace ShopSphere.Api.Features.Admin.Categories;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Policy = Policies.Admin)]
public class AdminCategoriesController : ControllerBase
{
    private readonly AdminCategoryService _categoryService;

    public AdminCategoriesController(AdminCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminCategoryDto>>> GetTree()
    {
        return Ok(await _categoryService.GetTreeAsync());
    }

    [HttpPost]
    public async Task<ActionResult<AdminCategoryDto>> Create(CategoryRequest request)
    {
        var category = await _categoryService.CreateAsync(request);
        return StatusCode(StatusCodes.Status201Created, category);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminCategoryDto>> Update(int id, CategoryRequest request)
    {
        return Ok(await _categoryService.UpdateAsync(id, request));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _categoryService.DeleteAsync(id);
        return NoContent();
    }
}
