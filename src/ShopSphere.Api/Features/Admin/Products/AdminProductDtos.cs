using System.ComponentModel.DataAnnotations;
using ShopSphere.Api.Features.Catalog;

namespace ShopSphere.Api.Features.Admin.Products;

public class AdminProductQuery
{
    [MaxLength(100)]
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    // null = all, true = active only, false = inactive only
    public bool? IsActive { get; set; }

    public bool? LowStock { get; set; }

    [RegularExpression("^(newest|name|price_asc|price_desc|stock_asc)$")]
    public string Sort { get; set; } = "newest";

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public class AdminProductListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; }
    public bool IsActive { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AdminProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int CategoryId { get; set; }
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; }
    public bool IsActive { get; set; }
    public List<ProductImageDto> Images { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Sent back on update so the API can detect edits made by someone else
    public string RowVersion { get; set; } = string.Empty;
}

public abstract class ProductRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    // Optional. Generated from the name when empty.
    [MaxLength(220), RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Slug may only contain lowercase letters, numbers and single dashes.")]
    public string? Slug { get; set; }

    [Required, MaxLength(50), RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "SKU may only contain letters, numbers and dashes.")]
    public string Sku { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public decimal Price { get; set; }

    [Range(0.01, 1_000_000)]
    public decimal? DiscountPrice { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Category is required.")]
    public int CategoryId { get; set; }

    [Range(0, 10_000)]
    public int LowStockThreshold { get; set; } = 5;

    public bool IsActive { get; set; } = true;
}

public class CreateProductRequest : ProductRequest
{
    [Range(0, 100_000)]
    public int StockQuantity { get; set; }
}

public class UpdateProductRequest : ProductRequest
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public class UploadProductImageRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [MaxLength(200)]
    public string? AltText { get; set; }
}
