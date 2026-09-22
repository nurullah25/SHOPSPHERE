using System.Text.RegularExpressions;

namespace ShopSphere.Api.Common;

public static class SlugHelper
{
    public static string Generate(string text)
    {
        var slug = text.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"[\s-]+", "-");
        return slug.Trim('-');
    }
}
