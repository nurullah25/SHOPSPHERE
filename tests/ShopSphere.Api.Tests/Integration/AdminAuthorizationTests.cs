using System.Net;
using ShopSphere.Api.Tests.Infrastructure;

namespace ShopSphere.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class AdminAuthorizationTests
{
    private readonly ShopSphereApiFactory _factory;

    public AdminAuthorizationTests(ShopSphereApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string> AdminEndpoints => new()
    {
        "/api/admin/products",
        "/api/admin/categories"
    };

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Anonymous_request_to_admin_endpoint_returns_401(string url)
    {
        var client = _factory.CreateApiClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Customer_request_to_admin_endpoint_returns_403(string url)
    {
        var client = await _factory.CreateCustomerClientAsync();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Admin_request_to_admin_endpoint_succeeds(string url)
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
