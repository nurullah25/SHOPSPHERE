namespace ShopSphere.Api.Entities;

public class Product : AuditableEntity
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Sku { get; set; } = null!;
    public string Description { get; set; } = null!;

    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }

    // Computed by SQL Server: COALESCE(DiscountPrice, Price)
    public decimal EffectivePrice { get; private set; }

    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public bool IsActive { get; set; } = true;

    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
