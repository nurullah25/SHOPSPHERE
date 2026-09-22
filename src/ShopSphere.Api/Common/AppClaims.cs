using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ShopSphere.Api.Common;

public static class AppClaims
{
    public const string Role = "role";
}

public static class Policies
{
    public const string Admin = "AdminOnly";
    public const string Customer = "CustomerOnly";
}

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedException("User id claim is missing.");
    }
}
