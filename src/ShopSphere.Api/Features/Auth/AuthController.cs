using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;

namespace ShopSphere.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    public const string RefreshCookieName = "shopsphere_refresh";

    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        SetRefreshCookie(result);
        return Ok(result.Response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        SetRefreshCookie(result);
        return Ok(result.Response);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh()
    {
        try
        {
            var result = await _authService.RefreshAsync(Request.Cookies[RefreshCookieName]);
            SetRefreshCookie(result);
            return Ok(result.Response);
        }
        catch (UnauthorizedException)
        {
            DeleteRefreshCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync(Request.Cookies[RefreshCookieName]);
        DeleteRefreshCookie();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        return Ok(await _authService.GetCurrentUserAsync(User.GetUserId()));
    }

    private void SetRefreshCookie(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, BuildCookieOptions(result.RefreshTokenExpiresAt));
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(null));
    }

    // HttpOnly: JavaScript can't read it. Path: only sent to /api/auth endpoints.
    private static CookieOptions BuildCookieOptions(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expires
    };
}
