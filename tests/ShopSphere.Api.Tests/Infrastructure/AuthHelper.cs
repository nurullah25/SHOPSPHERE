using System.Net.Http.Headers;
using System.Net.Http.Json;
using ShopSphere.Api.Features.Auth;

namespace ShopSphere.Api.Tests.Infrastructure;

public static class AuthHelper
{
    public const string DefaultPassword = "Password123";

    public static async Task<(AuthResponse Auth, string RefreshToken)> RegisterCustomerAsync(HttpClient client, string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email ?? $"customer-{Guid.NewGuid():N}@shopsphere.test",
            Password = DefaultPassword,
            FirstName = "Test",
            LastName = "Customer"
        });
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        return (auth, GetRefreshToken(response)!);
    }

    public static async Task<(AuthResponse Auth, string RefreshToken)> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        return (auth, GetRefreshToken(response)!);
    }

    public static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"{AuthController.RefreshCookieName}={refreshToken}");
        return client.SendAsync(request);
    }

    public static void UseBearerToken(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public static string? GetRefreshToken(HttpResponseMessage response) =>
        GetRefreshCookieHeader(response)?.Split(';')[0].Split('=', 2)[1];

    public static string? GetRefreshCookieHeader(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;

        return cookies.FirstOrDefault(c => c.StartsWith(AuthController.RefreshCookieName + "="));
    }
}
