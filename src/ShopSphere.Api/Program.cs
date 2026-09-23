using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Admin;
using ShopSphere.Api.Features.Admin.Categories;
using ShopSphere.Api.Features.Admin.Inventory;
using ShopSphere.Api.Features.Admin.Orders;
using ShopSphere.Api.Features.Admin.Products;
using ShopSphere.Api.Features.Auth;
using ShopSphere.Api.Features.Cart;
using ShopSphere.Api.Features.Catalog;
using ShopSphere.Api.Features.Checkout;
using ShopSphere.Api.Features.Coupons;
using ShopSphere.Api.Features.Orders;
using ShopSphere.Api.Features.Payments;
using ShopSphere.Api.Features.Wishlist;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services));

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddOptions<JwtSettings>()
        .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
        .Validate(s => s.Key.Length >= 32, "Jwt:Key must be set (user secrets) and be at least 32 characters long.")
        .ValidateOnStart();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

    // Configured through options so the values are read after all configuration
    // sources (user secrets, test overrides) have been loaded.
    builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<IOptions<JwtSettings>>((options, jwtOptions) =>
        {
            var jwt = jwtOptions.Value;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = AppClaims.Role
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(Policies.Admin, policy => policy.RequireRole(nameof(UserRole.Admin)))
        .AddPolicy(Policies.Customer, policy => policy.RequireRole(nameof(UserRole.Customer)));

    builder.Services.AddScoped<TokenService>();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<CatalogService>();
    builder.Services.AddScoped<CartService>();
    builder.Services.AddScoped<WishlistService>();
    builder.Services.AddScoped<CouponService>();
    builder.Services.AddScoped<CheckoutService>();
    builder.Services.AddScoped<OrderService>();
    builder.Services.AddScoped<OrderWorkflow>();
    builder.Services.AddScoped<AdminOrderService>();
    builder.Services.AddScoped<InventoryService>();
    builder.Services.AddScoped<PendingOrderExpiry>();
    builder.Services.Configure<OrderSettings>(builder.Configuration.GetSection(OrderSettings.SectionName));

    // Tests drive PendingOrderExpiry directly instead of waiting for the timer
    if (!builder.Environment.IsEnvironment("Testing"))
        builder.Services.AddHostedService<PendingOrderExpiryJob>();
    builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();
    builder.Services.Configure<ShippingSettings>(builder.Configuration.GetSection(ShippingSettings.SectionName));
    builder.Services.AddScoped<AuditService>();
    builder.Services.AddScoped<AdminProductService>();
    builder.Services.AddScoped<AdminCategoryService>();
    builder.Services.AddSingleton<ProductImageStorage>();

    // Enums are sent and received as names ("Shipped"), not numbers
    builder.Services.AddControllers()
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddHealthChecks();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "ShopSphere API", Version = "v1" });

        var bearerScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token returned by /api/auth/login",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };
        options.AddSecurityDefinition("Bearer", bearerScheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearerScheme] = Array.Empty<string>() });
    });

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        await DbSeeder.MigrateAndSeedAsync(app.Services);

        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "ShopSphere API failed to start");
}
finally
{
    Log.CloseAndFlush();
}

// Makes Program visible to WebApplicationFactory in the integration tests
public partial class Program;
