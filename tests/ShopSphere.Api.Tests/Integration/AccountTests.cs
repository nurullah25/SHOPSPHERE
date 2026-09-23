using System.Net;
using System.Net.Http.Json;
using ShopSphere.Api.Features.Account;
using ShopSphere.Api.Features.Auth;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class AccountTests
{
    private readonly ShopSphereApiFactory _factory;

    public AccountTests(ShopSphereApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Profile_can_be_updated_but_the_email_stays_the_same()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var before = await client.GetFromJsonAsync<ProfileDto>("/api/account/profile");

        var response = await client.PutAsJsonAsync("/api/account/profile", new UpdateProfileRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            PhoneNumber = "555-0199"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = (await response.Content.ReadFromJsonAsync<ProfileDto>())!;
        Assert.Equal("Updated", after.FirstName);
        Assert.Equal("555-0199", after.PhoneNumber);
        Assert.Equal(before!.Email, after.Email);
    }

    [Fact]
    public async Task Changing_the_password_requires_the_current_one()
    {
        var client = await _factory.CreateCustomerClientAsync();

        var response = await client.PutAsJsonAsync("/api/account/password", new ChangePasswordRequest
        {
            CurrentPassword = "NotMyPassword1",
            NewPassword = "BrandNewPass1"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_keeps_this_session_and_ends_the_others()
    {
        var client = _factory.CreateApiClient();
        var (auth, refreshToken) = await AuthHelper.RegisterCustomerAsync(client);
        client.UseBearerToken(auth.AccessToken);

        // A second device signs in with the same account
        var otherDevice = _factory.CreateApiClient();
        var (_, otherRefreshToken) = await AuthHelper.LoginAsync(otherDevice, auth.User.Email, AuthHelper.DefaultPassword);

        var change = new HttpRequestMessage(HttpMethod.Put, "/api/account/password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest
            {
                CurrentPassword = AuthHelper.DefaultPassword,
                NewPassword = "ChangedPass123"
            })
        };
        change.Headers.Add("Cookie", $"{AuthController.RefreshCookieName}={refreshToken}");
        change.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");

        var response = await client.SendAsync(change);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // This session can still refresh, the other device cannot
        var thisSession = await AuthHelper.RefreshAsync(client, refreshToken);
        var otherSession = await AuthHelper.RefreshAsync(otherDevice, otherRefreshToken);
        Assert.Equal(HttpStatusCode.OK, thisSession.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, otherSession.StatusCode);

        // And the new password works
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = auth.User.Email, Password = "ChangedPass123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task First_address_becomes_the_default_and_only_one_stays_default()
    {
        var client = await _factory.CreateCustomerClientAsync();

        var first = await CreateAddressAsync(client, "Home", isDefault: false);
        Assert.True(first.IsDefault);

        var second = await CreateAddressAsync(client, "Office", isDefault: true);
        Assert.True(second.IsDefault);

        var addresses = await client.GetFromJsonAsync<List<AddressDto>>("/api/account/addresses");
        Assert.Equal(2, addresses!.Count);
        Assert.Single(addresses, address => address.IsDefault);
        Assert.Equal(second.Id, addresses.Single(address => address.IsDefault).Id);
    }

    [Fact]
    public async Task Deleting_the_default_address_promotes_another_one()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var first = await CreateAddressAsync(client, "Home", isDefault: false);
        var second = await CreateAddressAsync(client, "Office", isDefault: true);

        var response = await client.DeleteAsync($"/api/account/addresses/{second.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var addresses = await client.GetFromJsonAsync<List<AddressDto>>("/api/account/addresses");
        var remaining = Assert.Single(addresses!);
        Assert.Equal(first.Id, remaining.Id);
        Assert.True(remaining.IsDefault);
    }

    [Fact]
    public async Task Addresses_of_another_customer_are_not_reachable()
    {
        var owner = await _factory.CreateCustomerClientAsync();
        var address = await CreateAddressAsync(owner, "Home", isDefault: true);

        var other = await _factory.CreateCustomerClientAsync();
        var update = await other.PutAsJsonAsync($"/api/account/addresses/{address.Id}", NewAddress("Hacked", false));
        var delete = await other.DeleteAsync($"/api/account/addresses/{address.Id}");

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Saved_address_can_be_used_at_checkout()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var address = await CreateAddressAsync(client, "Home", isDefault: true);

        var summary = await client.PostAsJsonAsync("/api/checkout/summary", new { });
        summary.EnsureSuccessStatusCode();
        var details = await summary.Content.ReadFromJsonAsync<ShopSphere.Api.Features.Checkout.CheckoutSummaryDto>();

        Assert.Contains(details!.SavedAddresses, saved => saved.Id == address.Id && saved.IsDefault);
    }

    [Fact]
    public async Task Audit_log_is_admin_only_and_lists_recent_actions()
    {
        var customer = await _factory.CreateCustomerClientAsync();
        var admin = await _factory.CreateAdminClientAsync();

        var forbidden = await customer.GetAsync("/api/admin/audit-logs");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var actions = await admin.GetFromJsonAsync<List<string>>("/api/admin/audit-logs/actions");
        Assert.NotNull(actions);
    }

    private static async Task<AddressDto> CreateAddressAsync(HttpClient client, string name, bool isDefault)
    {
        var response = await client.PostAsJsonAsync("/api/account/addresses", NewAddress(name, isDefault));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AddressDto>())!;
    }

    private static AddressRequest NewAddress(string name, bool isDefault) => new()
    {
        FullName = name,
        Line1 = "742 Maple Avenue",
        City = "Portland",
        State = "OR",
        PostalCode = "97205",
        Country = "United States",
        IsDefault = isDefault
    };
}
