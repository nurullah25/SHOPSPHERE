using System.ComponentModel.DataAnnotations;

namespace ShopSphere.Api.Features.Admin.Categories;

public class AdminCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }

    // Products directly in this category, not counting subcategories
    public int ProductCount { get; set; }

    public List<AdminCategoryDto> Children { get; set; } = new();
}

public class CategoryRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120), RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Slug may only contain lowercase letters, numbers and single dashes.")]
    public string? Slug { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public int? ParentId { get; set; }

    [Range(0, 1000)]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
