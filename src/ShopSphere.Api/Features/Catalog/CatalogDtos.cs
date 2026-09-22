using System.ComponentModel.DataAnnotations;

namespace ShopSphere.Api.Features.Catalog;

public class CategoryNodeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CategoryNodeDto> Children { get; set; } = new();
}

public class CategoryLinkDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public class CategoryDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CategoryLinkDto> Breadcrumb { get; set; } = new();
    public List<CategoryLinkDto> Children { get; set; } = new();
}

public class ProductQuery : IValidatableObject
{
    public const string SortPattern = "^(newest|price_asc|price_desc|name|rating)$";

    [MaxLength(100)]
    public string? Search { get; set; }

    // Category slug. Products in subcategories are included.
    public string? Category { get; set; }

    [Range(0, 1_000_000)]
    public decimal? MinPrice { get; set; }

    [Range(0, 1_000_000)]
    public decimal? MaxPrice { get; set; }

    public bool? InStock { get; set; }

    [RegularExpression(SortPattern, ErrorMessage = "Sort must be one of: newest, price_asc, price_desc, name, rating.")]
    public string Sort { get; set; } = "newest";

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 48)]
    public int PageSize { get; set; } = 12;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinPrice.HasValue && MaxPrice.HasValue && MinPrice > MaxPrice)
            yield return new ValidationResult("Minimum price can't be greater than maximum price.", new[] { nameof(MinPrice) });
    }
}

public class ProductListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public string? ImageUrl { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public bool InStock { get; set; }
    public bool LowStock { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

public class ProductImageDto
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public bool IsMain { get; set; }
}

public class ProductDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public int StockQuantity { get; set; }
    public bool LowStock { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public List<ProductImageDto> Images { get; set; } = new();
    public List<CategoryLinkDto> Breadcrumb { get; set; } = new();
}
