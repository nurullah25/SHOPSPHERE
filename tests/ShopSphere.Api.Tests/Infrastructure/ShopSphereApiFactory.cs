using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.Api.Data;

namespace ShopSphere.Api.Tests.Infrastructure;

// Runs the real API in memory against a dedicated LocalDB database.
// SQL Server is used instead of the EF in-memory provider because the business
// rules depend on transactions, constraints and ExecuteUpdate.
public class ShopSphereApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@shopsphere.test";
    public const string AdminPassword = "Admin#12345";
    public const string CustomerEmail = "demo@shopsphere.test";
    public const string CustomerPassword = "Customer#12345";

    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=ShopSphere_Tests;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Jwt:Key"] = "integration-tests-signing-key-that-is-long-enough",
                ["Seed:AdminEmail"] = AdminEmail,
                ["Seed:AdminPassword"] = AdminPassword,
                ["Seed:CustomerEmail"] = CustomerEmail,
                ["Seed:CustomerPassword"] = CustomerPassword,
                ["Serilog:MinimumLevel:Default"] = "Warning"
            });
        });
    }

    public async Task InitializeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
        }

        await DbSeeder.MigrateAndSeedAsync(Services);
    }

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        // The refresh cookie is marked Secure, so the client has to use https
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false
    });

    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateApiClient();
        var (auth, _) = await AuthHelper.LoginAsync(client, AdminEmail, AdminPassword);
        client.UseBearerToken(auth.AccessToken);
        return client;
    }

    public async Task<HttpClient> CreateCustomerClientAsync()
    {
        var client = CreateApiClient();
        var (auth, _) = await AuthHelper.RegisterCustomerAsync(client);
        client.UseBearerToken(auth.AccessToken);
        return client;
    }

    // For arranging data the API doesn't expose yet, or for checking side effects
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ShopSphereApiFactory>
{
    public const string Name = "Api";
}
