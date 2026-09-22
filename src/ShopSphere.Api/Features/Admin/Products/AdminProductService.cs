using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Catalog;

namespace ShopSphere.Api.Features.Admin.Products;

public class AdminProductService
{
    private const int MaxImagesPerProduct = 8;

    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly ProductImageStorage _imageStorage;

    public AdminProductService(AppDbContext db, AuditService audit, ProductImageStorage imageStorage)
    {
        _db = db;
        _audit = audit;
        _imageStorage = imageStorage;
    }

    public async Task<PagedResult<AdminProductListItemDto>> GetProductsAsync(AdminProductQuery query)
    {
        var products = _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(p => p.Name.Contains(term) || p.Sku.Contains(term));
        }

        if (query.CategoryId.HasValue)
            products = products.Where(p => p.CategoryId == query.CategoryId);

        if (query.IsActive.HasValue)
            products = products.Where(p => p.IsActive == query.IsActive);

        if (query.LowStock == true)
            products = products.Where(p => p.StockQuantity <= p.LowStockThreshold);

        IQueryable<Product> sorted = query.Sort switch
        {
            "name" => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
            "price_asc" => products.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "price_desc" => products.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            "stock_asc" => products.OrderBy(p => p.StockQuantity).ThenBy(p => p.Id),
            _ => products.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        };

        return await sorted
            .Select(p => new AdminProductListItemDto
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                Slug = p.Slug,
                CategoryName = p.Category.Name,
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                StockQuantity = p.StockQuantity,
                LowStockThreshold = p.LowStockThreshold,
                IsActive = p.IsActive,
                ImageUrl = p.Images.Where(i => i.IsMain).Select(i => i.Url).FirstOrDefault(),
                UpdatedAt = p.UpdatedAt
            })
            .ToPagedResultAsync(query.Page, query.PageSize);
    }

    public async Task<AdminProductDto> GetProductAsync(int id)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .SingleOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Product {id} was not found.");

        return ToDto(product);
    }

    public async Task<AdminProductDto> CreateAsync(CreateProductRequest request, int adminId)
    {
        await ValidateAsync(request, productId: null);

        var product = new Product
        {
            Name = request.Name.Trim(),
            Slug = await ResolveSlugAsync(request.Slug, request.Name, productId: null),
            Sku = NormalizeSku(request.Sku),
            Description = request.Description.Trim(),
            Price = request.Price,
            DiscountPrice = request.DiscountPrice,
            CategoryId = request.CategoryId,
            StockQuantity = request.StockQuantity,
            LowStockThreshold = request.LowStockThreshold,
            IsActive = request.IsActive
        };
        _db.Products.Add(product);

        if (request.StockQuantity > 0)
        {
            _db.InventoryMovements.Add(new InventoryMovement
            {
                Product = product,
                QuantityChange = request.StockQuantity,
                QuantityAfter = request.StockQuantity,
                Reason = InventoryChangeReason.Restock,
                Note = "Opening stock",
                UserId = adminId
            });
        }

        await _db.SaveChangesAsync();

        _audit.Record(adminId, "ProductCreated", nameof(Product), product.Id, new { product.Sku, product.Price });
        await _db.SaveChangesAsync();

        return await GetProductAsync(product.Id);
    }

    public async Task<AdminProductDto> UpdateAsync(int id, UpdateProductRequest request, int adminId)
    {
        var product = await _db.Products.SingleOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Product {id} was not found.");

        await ValidateAsync(request, product.Id);

        // EF compares this with the current value in the WHERE clause of the UPDATE.
        // If another admin saved in between, no row matches and a 409 is returned.
        _db.Entry(product).Property(p => p.RowVersion).OriginalValue = DecodeRowVersion(request.RowVersion);

        var priceBefore = product.Price;
        var discountBefore = product.DiscountPrice;

        product.Name = request.Name.Trim();
        product.Sku = NormalizeSku(request.Sku);
        product.Description = request.Description.Trim();
        product.Price = request.Price;
        product.DiscountPrice = request.DiscountPrice;
        product.CategoryId = request.CategoryId;
        product.LowStockThreshold = request.LowStockThreshold;
        product.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.Slug))
            product.Slug = await ResolveSlugAsync(request.Slug, request.Name, product.Id);

        _audit.Record(adminId, "ProductUpdated", nameof(Product), product.Id, new
        {
            PriceBefore = priceBefore,
            PriceAfter = product.Price,
            DiscountBefore = discountBefore,
            DiscountAfter = product.DiscountPrice,
            product.IsActive
        });

        await _db.SaveChangesAsync();

        return await GetProductAsync(product.Id);
    }

    public async Task DeleteAsync(int id, int adminId)
    {
        var product = await _db.Products
            .Include(p => p.Images)
            .SingleOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Product {id} was not found.");

        if (await _db.OrderItems.AnyAsync(i => i.ProductId == id))
        {
            throw new BusinessRuleException("Product has orders",
                "This product appears in past orders and can't be deleted. Deactivate it instead.");
        }

        var imageUrls = product.Images.Select(i => i.Url).ToList();

        _db.Products.Remove(product);
        _audit.Record(adminId, "ProductDeleted", nameof(Product), id, new { product.Sku, product.Name });
        await _db.SaveChangesAsync();

        foreach (var url in imageUrls)
            _imageStorage.Delete(url);
    }

    public async Task<ProductImageDto> AddImageAsync(int productId, UploadProductImageRequest request)
    {
        var product = await _db.Products
            .Include(p => p.Images)
            .SingleOrDefaultAsync(p => p.Id == productId)
            ?? throw new NotFoundException($"Product {productId} was not found.");

        if (product.Images.Count >= MaxImagesPerProduct)
            throw new BusinessRuleException("Too many images", $"A product can have at most {MaxImagesPerProduct} images.");

        var url = await _imageStorage.SaveAsync(request.File);

        var image = new ProductImage
        {
            Url = url,
            AltText = string.IsNullOrWhiteSpace(request.AltText) ? product.Name : request.AltText.Trim(),
            SortOrder = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.SortOrder) + 1,
            IsMain = product.Images.Count == 0
        };
        product.Images.Add(image);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            _imageStorage.Delete(url);
            throw;
        }

        return new ProductImageDto { Id = image.Id, Url = image.Url, AltText = image.AltText, IsMain = image.IsMain };
    }

    public async Task SetMainImageAsync(int productId, int imageId)
    {
        var images = await _db.ProductImages.Where(i => i.ProductId == productId).ToListAsync();
        if (images.All(i => i.Id != imageId))
            throw new NotFoundException($"Image {imageId} was not found for product {productId}.");

        foreach (var image in images)
            image.IsMain = image.Id == imageId;

        await _db.SaveChangesAsync();
    }

    public async Task DeleteImageAsync(int productId, int imageId)
    {
        var images = await _db.ProductImages
            .Where(i => i.ProductId == productId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();

        var image = images.SingleOrDefault(i => i.Id == imageId)
            ?? throw new NotFoundException($"Image {imageId} was not found for product {productId}.");

        _db.ProductImages.Remove(image);

        if (image.IsMain)
        {
            var next = images.FirstOrDefault(i => i.Id != imageId);
            if (next != null)
                next.IsMain = true;
        }

        await _db.SaveChangesAsync();
        _imageStorage.Delete(image.Url);
    }

    private async Task ValidateAsync(ProductRequest request, int? productId)
    {
        if (request.DiscountPrice.HasValue && request.DiscountPrice >= request.Price)
            throw new BusinessRuleException("Invalid discount", "Discount price must be lower than the regular price.", StatusCodes.Status400BadRequest);

        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId))
            throw new BusinessRuleException("Invalid category", $"Category {request.CategoryId} doesn't exist.", StatusCodes.Status400BadRequest);

        var sku = NormalizeSku(request.Sku);
        if (await _db.Products.AnyAsync(p => p.Sku == sku && p.Id != productId))
            throw new BusinessRuleException("Duplicate SKU", $"Another product already uses SKU {sku}.");
    }

    private async Task<string> ResolveSlugAsync(string? requestedSlug, string name, int? productId)
    {
        if (!string.IsNullOrWhiteSpace(requestedSlug))
        {
            if (await _db.Products.AnyAsync(p => p.Slug == requestedSlug && p.Id != productId))
                throw new BusinessRuleException("Duplicate slug", $"Another product already uses the URL '{requestedSlug}'.");

            return requestedSlug;
        }

        var baseSlug = SlugHelper.Generate(name);
        if (baseSlug.Length == 0)
            baseSlug = "product";

        var slug = baseSlug;
        var suffix = 2;
        while (await _db.Products.AnyAsync(p => p.Slug == slug && p.Id != productId))
            slug = $"{baseSlug}-{suffix++}";

        return slug;
    }

    private static byte[] DecodeRowVersion(string rowVersion)
    {
        try
        {
            return Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new BusinessRuleException("Invalid row version", "The RowVersion value is not valid.", StatusCodes.Status400BadRequest);
        }
    }

    private static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    private static AdminProductDto ToDto(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Slug = product.Slug,
        Sku = product.Sku,
        Description = product.Description,
        Price = product.Price,
        DiscountPrice = product.DiscountPrice,
        CategoryId = product.CategoryId,
        StockQuantity = product.StockQuantity,
        LowStockThreshold = product.LowStockThreshold,
        IsActive = product.IsActive,
        Images = product.Images
            .OrderByDescending(i => i.IsMain).ThenBy(i => i.SortOrder)
            .Select(i => new ProductImageDto { Id = i.Id, Url = i.Url, AltText = i.AltText, IsMain = i.IsMain })
            .ToList(),
        CreatedAt = product.CreatedAt,
        UpdatedAt = product.UpdatedAt,
        RowVersion = Convert.ToBase64String(product.RowVersion)
    };
}
