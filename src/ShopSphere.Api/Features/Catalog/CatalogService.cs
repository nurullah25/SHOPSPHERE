using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Catalog;

public class CatalogService
{
    private const int MaxSearchTerms = 5;

    private readonly AppDbContext _db;

    public CatalogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<CategoryNodeDto>> GetCategoryTreeAsync()
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync();

        var childrenByParent = categories.ToLookup(c => c.ParentId);

        List<CategoryNodeDto> BuildLevel(int? parentId) => childrenByParent[parentId]
            .Select(c => new CategoryNodeDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                Children = BuildLevel(c.Id)
            })
            .ToList();

        return BuildLevel(null);
    }

    public async Task<CategoryDetailDto> GetCategoryAsync(string slug)
    {
        var categories = await _db.Categories.AsNoTracking().ToListAsync();
        var category = categories.FirstOrDefault(c => c.Slug == slug && c.IsActive)
            ?? throw new NotFoundException($"Category '{slug}' was not found.");

        return new CategoryDetailDto
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            Breadcrumb = CategoryHierarchy.GetPath(categories, category.Id).Select(ToLink).ToList(),
            Children = categories
                .Where(c => c.ParentId == category.Id && c.IsActive)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                .Select(ToLink)
                .ToList()
        };
    }

    public async Task<PagedResult<ProductListItemDto>> SearchProductsAsync(ProductQuery query)
    {
        var products = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Category.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var categories = await _db.Categories.AsNoTracking().ToListAsync();
            var category = categories.FirstOrDefault(c => c.Slug == query.Category && c.IsActive)
                ?? throw new NotFoundException($"Category '{query.Category}' was not found.");

            var categoryIds = CategoryHierarchy.GetSelfAndDescendantIds(categories, category.Id);
            products = products.Where(p => categoryIds.Contains(p.CategoryId));
        }

        // Every word has to match somewhere, so "oak shelf" narrows the results
        foreach (var term in SplitSearchTerms(query.Search))
        {
            products = products.Where(p =>
                p.Name.Contains(term) || p.Sku.Contains(term) || p.Description.Contains(term));
        }

        if (query.MinPrice.HasValue)
            products = products.Where(p => p.EffectivePrice >= query.MinPrice.Value);

        if (query.MaxPrice.HasValue)
            products = products.Where(p => p.EffectivePrice <= query.MaxPrice.Value);

        if (query.InStock == true)
            products = products.Where(p => p.StockQuantity > 0);

        // Id is always the last sort key so paging is stable when values tie
        IQueryable<Product> sorted = query.Sort switch
        {
            "price_asc" => products.OrderBy(p => p.EffectivePrice).ThenBy(p => p.Id),
            "price_desc" => products.OrderByDescending(p => p.EffectivePrice).ThenBy(p => p.Id),
            "name" => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
            "rating" => products.OrderByDescending(p => p.AverageRating).ThenByDescending(p => p.ReviewCount).ThenBy(p => p.Id),
            _ => products.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        };

        return await sorted.Select(ToListItem).ToPagedResultAsync(query.Page, query.PageSize);
    }

    public async Task<ProductDetailDto> GetProductAsync(string slug)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .SingleOrDefaultAsync(p => p.Slug == slug && p.IsActive && p.Category.IsActive)
            ?? throw new NotFoundException($"Product '{slug}' was not found.");

        var categories = await _db.Categories.AsNoTracking().ToListAsync();

        return new ProductDetailDto
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Sku = product.Sku,
            Description = product.Description,
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            EffectivePrice = product.EffectivePrice,
            StockQuantity = product.StockQuantity,
            LowStock = product.StockQuantity > 0 && product.StockQuantity <= product.LowStockThreshold,
            AverageRating = product.AverageRating,
            ReviewCount = product.ReviewCount,
            Images = product.Images
                .OrderByDescending(i => i.IsMain).ThenBy(i => i.SortOrder)
                .Select(i => new ProductImageDto { Id = i.Id, Url = i.Url, AltText = i.AltText, IsMain = i.IsMain })
                .ToList(),
            Breadcrumb = CategoryHierarchy.GetPath(categories, product.CategoryId).Select(ToLink).ToList()
        };
    }

    public static readonly Expression<Func<Product, ProductListItemDto>> ToListItem = p => new ProductListItemDto
    {
        Id = p.Id,
        Name = p.Name,
        Slug = p.Slug,
        Price = p.Price,
        DiscountPrice = p.DiscountPrice,
        EffectivePrice = p.EffectivePrice,
        ImageUrl = p.Images.Where(i => i.IsMain).Select(i => i.Url).FirstOrDefault(),
        CategoryName = p.Category.Name,
        CategorySlug = p.Category.Slug,
        InStock = p.StockQuantity > 0,
        LowStock = p.StockQuantity > 0 && p.StockQuantity <= p.LowStockThreshold,
        AverageRating = p.AverageRating,
        ReviewCount = p.ReviewCount
    };

    private static IEnumerable<string> SplitSearchTerms(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? Enumerable.Empty<string>()
            : search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(MaxSearchTerms);

    private static CategoryLinkDto ToLink(Category category) => new() { Name = category.Name, Slug = category.Slug };
}
