using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Features.Auth;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class AuthTests
{
    private readonly ShopSphereApiFactory _factory;

    public AuthTests(ShopSphereApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_returns_access_token_and_sets_http_only_refresh_cookie()
    {
        var client = _factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = $"new-{Guid.NewGuid():N}@shopsphere.test",
            Password = "Password123",
            FirstName = "Jane",
            LastName = "Doe"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrEmpty(auth!.AccessToken));
        Assert.Equal("Customer", auth.User.Role);

        var cookie = AuthHelper.GetRefreshCookieHeader(response)!.ToLowerInvariant();
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("path=/api/auth", cookie);
    }

    [Fact]
    public async Task Register_with_existing_email_returns_conflict()
    {
        var client = _factory.CreateApiClient();
        var email = $"taken-{Guid.NewGuid():N}@shopsphere.test";
        await AuthHelper.RegisterCustomerAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email.ToUpperInvariant(),
            Password = "Password123",
            FirstName = "Jane",
            LastName = "Doe"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_weak_password_returns_validation_problem()
    {
        var client = _factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = $"weak-{Guid.NewGuid():N}@shopsphere.test",
            Password = "password",
            FirstName = "Jane",
            LastName = "Doe"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.True(problem!.Errors.ContainsKey("Password"));
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_unauthorized_problem()
    {
        var client = _factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = ShopSphereApiFactory.CustomerEmail,
            Password = "WrongPassword1"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid email or password.", problem!.Detail);
        Assert.Null(AuthHelper.GetRefreshCookieHeader(response));
    }

    [Fact]
    public async Task Seeded_admin_gets_admin_role_in_login_response()
    {
        var client = _factory.CreateApiClient();

        var (auth, _) = await AuthHelper.LoginAsync(client, ShopSphereApiFactory.AdminEmail, ShopSphereApiFactory.AdminPassword);

        Assert.Equal("Admin", auth.User.Role);
    }

    [Fact]
    public async Task Me_requires_a_valid_access_token()
    {
        var client = _factory.CreateApiClient();

        var anonymous = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var (auth, _) = await AuthHelper.RegisterCustomerAsync(client);
        client.UseBearerToken(auth.AccessToken);

        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me");
        Assert.Equal(auth.User.Email, me!.Email);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_the_old_token_stops_working()
    {
        var client = _factory.CreateApiClient();
        var (_, originalToken) = await AuthHelper.RegisterCustomerAsync(client);

        var refreshed = await AuthHelper.RefreshAsync(client, originalToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        var rotatedToken = AuthHelper.GetRefreshToken(refreshed);
        Assert.NotNull(rotatedToken);
        Assert.NotEqual(originalToken, rotatedToken);

        var reused = await AuthHelper.RefreshAsync(client, originalToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_all_sessions()
    {
        var client = _factory.CreateApiClient();
        var (_, stolenToken) = await AuthHelper.RegisterCustomerAsync(client);

        // The legitimate user refreshes first...
        var refreshed = await AuthHelper.RefreshAsync(client, stolenToken);
        var currentToken = AuthHelper.GetRefreshToken(refreshed)!;

        // ...then the old token shows up again
        await AuthHelper.RefreshAsync(client, stolenToken);

        var afterReuse = await AuthHelper.RefreshAsync(client, currentToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var client = _factory.CreateApiClient();
        var (_, refreshToken) = await AuthHelper.RegisterCustomerAsync(client);

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", $"{AuthController.RefreshCookieName}={refreshToken}");
        var logoutResponse = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refresh = await AuthHelper.RefreshAsync(client, refreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
