using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Catalog;

namespace ShopSphere.Api.Features.Admin.Categories;

public class AdminCategoryService
{
    private readonly AppDbContext _db;

    public AdminCategoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<AdminCategoryDto>> GetTreeAsync()
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync();

        var productCounts = await _db.Products
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

        var childrenByParent = categories.ToLookup(c => c.ParentId);

        List<AdminCategoryDto> BuildLevel(int? parentId) => childrenByParent[parentId]
            .Select(c => new AdminCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ParentId = c.ParentId,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                ProductCount = productCounts.GetValueOrDefault(c.Id),
                Children = BuildLevel(c.Id)
            })
            .ToList();

        return BuildLevel(null);
    }

    public async Task<AdminCategoryDto> CreateAsync(CategoryRequest request)
    {
        if (request.ParentId.HasValue && !await _db.Categories.AnyAsync(c => c.Id == request.ParentId))
            throw InvalidParent("Parent category doesn't exist.");

        var category = new Category
        {
            Name = request.Name.Trim(),
            Slug = await ResolveSlugAsync(request.Slug, request.Name, categoryId: null),
            Description = request.Description?.Trim(),
            ParentId = request.ParentId,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return ToDto(category, productCount: 0);
    }

    public async Task<AdminCategoryDto> UpdateAsync(int id, CategoryRequest request)
    {
        var categories = await _db.Categories.ToListAsync();
        var category = categories.SingleOrDefault(c => c.Id == id)
            ?? throw new NotFoundException($"Category {id} was not found.");

        if (request.ParentId.HasValue)
        {
            if (categories.All(c => c.Id != request.ParentId))
                throw InvalidParent("Parent category doesn't exist.");

            // Moving a category under itself or one of its own children would create a loop
            if (CategoryHierarchy.GetSelfAndDescendantIds(categories, id).Contains(request.ParentId.Value))
                throw InvalidParent("A category can't be moved under itself or one of its subcategories.");
        }

        category.Name = request.Name.Trim();
        category.Description = request.Description?.Trim();
        category.ParentId = request.ParentId;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.Slug))
            category.Slug = await ResolveSlugAsync(request.Slug, request.Name, category.Id);

        await _db.SaveChangesAsync();

        var productCount = await _db.Products.CountAsync(p => p.CategoryId == id);
        return ToDto(category, productCount);
    }

    public async Task DeleteAsync(int id)
    {
        var category = await _db.Categories.SingleOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Category {id} was not found.");

        if (await _db.Categories.AnyAsync(c => c.ParentId == id))
            throw new BusinessRuleException("Category has subcategories", "Move or delete the subcategories first.");

        if (await _db.Products.AnyAsync(p => p.CategoryId == id))
            throw new BusinessRuleException("Category has products", "Move the products to another category or deactivate this category instead.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
    }

    private async Task<string> ResolveSlugAsync(string? requestedSlug, string name, int? categoryId)
    {
        var slug = string.IsNullOrWhiteSpace(requestedSlug) ? SlugHelper.Generate(name) : requestedSlug;
        if (slug.Length == 0)
            throw new BusinessRuleException("Invalid name", "Category name must contain letters or numbers.", StatusCodes.Status400BadRequest);

        if (await _db.Categories.AnyAsync(c => c.Slug == slug && c.Id != categoryId))
            throw new BusinessRuleException("Duplicate slug", $"Another category already uses the URL '{slug}'.");

        return slug;
    }

    private static BusinessRuleException InvalidParent(string message) =>
        new("Invalid parent category", message, StatusCodes.Status400BadRequest);

    private static AdminCategoryDto ToDto(Category category, int productCount) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Slug = category.Slug,
        Description = category.Description,
        ParentId = category.ParentId,
        SortOrder = category.SortOrder,
        IsActive = category.IsActive,
        ProductCount = productCount
    };
}
