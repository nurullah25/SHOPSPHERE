using ShopSphere.Api.Common;

namespace ShopSphere.Api.Features.Admin.Products;

// Stores product images on local disk under wwwroot/uploads/products.
// Swapping this for blob storage later only touches this class.
public class ProductImageStorage
{
    private const long MaxFileSize = 2 * 1024 * 1024;

    private static readonly Dictionary<string, string> ExtensionsByContentType = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private readonly string _uploadsPath;

    public ProductImageStorage(IWebHostEnvironment environment)
    {
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        _uploadsPath = Path.Combine(webRoot, "uploads", "products");
    }

    public async Task<string> SaveAsync(IFormFile file)
    {
        if (file.Length == 0)
            throw InvalidImage("The uploaded file is empty.");

        if (file.Length > MaxFileSize)
            throw InvalidImage("Images must be 2 MB or smaller.");

        if (!ExtensionsByContentType.TryGetValue(file.ContentType, out var extension))
            throw InvalidImage("Only JPEG, PNG and WebP images are allowed.");

        await using var input = file.OpenReadStream();

        // Don't trust the content type header alone, check the file's first bytes
        var header = new byte[12];
        var bytesRead = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        if (DetectExtension(header.AsSpan(0, bytesRead)) != extension)
            throw InvalidImage("The file content doesn't match an image of the declared type.");

        input.Seek(0, SeekOrigin.Begin);

        Directory.CreateDirectory(_uploadsPath);
        var fileName = $"{Guid.NewGuid():N}{extension}";

        await using var output = File.Create(Path.Combine(_uploadsPath, fileName));
        await input.CopyToAsync(output);

        return $"/uploads/products/{fileName}";
    }

    public void Delete(string url)
    {
        // Only the file name is used, so a stored URL can't point outside the folder
        var path = Path.Combine(_uploadsPath, Path.GetFileName(url));
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ".jpg";

        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ".png";

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
            return ".webp";

        return null;
    }

    private static BusinessRuleException InvalidImage(string message) =>
        new("Invalid image", message, StatusCodes.Status400BadRequest);
}
