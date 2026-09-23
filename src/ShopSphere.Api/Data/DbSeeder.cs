using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Data;

public static class DbSeeder
{
    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync())
        {
            SeedUsers(db, config);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded demo users");
        }

        if (!await db.Categories.AnyAsync())
        {
            SeedCatalog(db);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded categories and products");
        }

        if (!await db.Coupons.AnyAsync())
        {
            SeedCoupons(db);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded coupons");
        }

        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        // Demo order history makes the dashboard and reports meaningful when
        // someone runs the project for the first time. Tests seed their own data.
        if (environment.IsDevelopment() && !await db.Orders.AnyAsync())
        {
            await SeedDemoOrdersAsync(db);
            await SeedDemoReviewsAsync(db);
            logger.LogInformation("Seeded demo order history and reviews");
        }

        var imagesAdded = await AddDemoImagesAsync(db, environment);
        if (imagesAdded > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Attached {Count} demo product images", imagesAdded);
        }
    }

    private static async Task SeedDemoOrdersAsync(AppDbContext db)
    {
        const decimal flatShipping = 5.99m;
        const decimal freeShippingThreshold = 75m;

        var customer = await db.Users.FirstAsync(u => u.Role == UserRole.Customer);
        var address = await db.Addresses.FirstAsync(a => a.UserId == customer.Id);
        var products = await db.Products.Where(p => p.IsActive && p.StockQuantity > 3).ToListAsync();

        // Fixed seed so everyone who clones the project sees the same history
        var random = new Random(20260923);
        var statuses = new[]
        {
            OrderStatus.Delivered, OrderStatus.Delivered, OrderStatus.Delivered, OrderStatus.Delivered,
            OrderStatus.Shipped, OrderStatus.Shipped, OrderStatus.Processing, OrderStatus.Confirmed,
            OrderStatus.Cancelled
        };

        for (var daysAgo = 29; daysAgo >= 1; daysAgo--)
        {
            // Not every day has orders
            if (random.Next(0, 10) < 4)
                continue;

            var placedAt = DateTime.UtcNow.Date.AddDays(-daysAgo).AddHours(random.Next(9, 20)).AddMinutes(random.Next(0, 60));
            var status = statuses[random.Next(statuses.Length)];

            var order = new Order
            {
                UserId = customer.Id,
                Status = status,
                PlacedAt = placedAt,
                CreatedAt = placedAt,
                ShippingAddress = new OrderAddress
                {
                    FullName = address.FullName,
                    Line1 = address.Line1,
                    Line2 = address.Line2,
                    City = address.City,
                    State = address.State,
                    PostalCode = address.PostalCode,
                    Country = address.Country,
                    PhoneNumber = address.PhoneNumber
                }
            };

            foreach (var product in products.OrderBy(_ => random.Next()).Take(random.Next(1, 4)))
            {
                var quantity = random.Next(1, 3);
                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    UnitPrice = product.EffectivePrice,
                    Quantity = quantity,
                    LineTotal = product.EffectivePrice * quantity
                });

                // Cancelled orders released their stock again, so only the
                // others actually took it
                if (status != OrderStatus.Cancelled)
                {
                    product.StockQuantity = Math.Max(0, product.StockQuantity - quantity);
                    db.InventoryMovements.Add(new InventoryMovement
                    {
                        ProductId = product.Id,
                        QuantityChange = -quantity,
                        QuantityAfter = product.StockQuantity,
                        Reason = InventoryChangeReason.Sale,
                        Order = order,
                        UserId = customer.Id,
                        CreatedAt = placedAt
                    });
                }
            }

            order.Subtotal = order.Items.Sum(i => i.LineTotal);
            order.ShippingCost = order.Subtotal >= freeShippingThreshold ? 0m : flatShipping;
            order.Total = order.Subtotal + order.ShippingCost;

            order.Payments.Add(new Payment
            {
                Amount = order.Total,
                Status = status == OrderStatus.Cancelled ? PaymentStatus.Refunded : PaymentStatus.Succeeded,
                Provider = "Mock",
                TransactionReference = $"mock_seed_{daysAgo:00}",
                CreatedAt = placedAt,
                ProcessedAt = placedAt
            });

            AddStatusHistory(order, status, placedAt);

            if (status == OrderStatus.Cancelled)
            {
                order.CancelledAt = placedAt.AddHours(2);
                order.CancellationReason = "Cancelled by customer";
            }

            db.Orders.Add(order);
        }

        await db.SaveChangesAsync();
    }

    // A few reviews so product pages aren't empty. Only products the demo
    // customer actually received, which is the rule the API enforces.
    private static async Task SeedDemoReviewsAsync(AppDbContext db)
    {
        var comments = new (int Rating, string Title, string Comment)[]
        {
            (5, "Exactly what I wanted", "Arrived quickly and looks even better in person."),
            (4, "Good quality for the price", "Solid build. Took off a star because the colour is slightly darker than the photo."),
            (5, "Would buy again", "Second one I've ordered, the first has held up for months."),
            (4, "Happy with it", "Does the job nicely, packaging could be less bulky."),
            (5, "Great value", "Feels much more expensive than it was."),
            (3, "Fine, not amazing", "Works as described but the finish scratches easily.")
        };

        var customerId = await db.Users.Where(u => u.Role == UserRole.Customer).Select(u => u.Id).FirstAsync();

        var productIds = await db.OrderItems
            .Where(i => i.Order.UserId == customerId && i.Order.Status == OrderStatus.Delivered)
            .Select(i => i.ProductId)
            .Distinct()
            .Take(comments.Length)
            .ToListAsync();

        for (var i = 0; i < productIds.Count; i++)
        {
            var (rating, title, comment) = comments[i];
            db.Reviews.Add(new Review
            {
                ProductId = productIds[i],
                UserId = customerId,
                Rating = rating,
                Title = title,
                Comment = comment,
                CreatedAt = DateTime.UtcNow.AddDays(-i - 1)
            });
        }

        await db.SaveChangesAsync();

        // Keep the rating shown on product cards in step with the reviews
        foreach (var productId in productIds)
        {
            var stats = await db.Reviews
                .Where(r => r.ProductId == productId)
                .GroupBy(r => r.ProductId)
                .Select(g => new { Count = g.Count(), Average = g.Average(r => (double)r.Rating) })
                .SingleAsync();

            await db.Products
                .Where(p => p.Id == productId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.ReviewCount, stats.Count)
                    .SetProperty(p => p.AverageRating, Math.Round((decimal)stats.Average, 2)));
        }
    }

    // Walks the order through the statuses it must have passed through
    private static void AddStatusHistory(Order order, OrderStatus finalStatus, DateTime placedAt)
    {
        var timeline = new List<OrderStatus> { OrderStatus.Pending };

        if (finalStatus == OrderStatus.Cancelled)
        {
            timeline.Add(OrderStatus.Confirmed);
            timeline.Add(OrderStatus.Cancelled);
        }
        else
        {
            foreach (var status in new[] { OrderStatus.Confirmed, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered })
            {
                timeline.Add(status);
                if (status == finalStatus)
                    break;
            }
        }

        OrderStatus? previous = null;
        for (var i = 0; i < timeline.Count; i++)
        {
            order.StatusHistory.Add(new OrderStatusHistory
            {
                FromStatus = previous,
                ToStatus = timeline[i],
                ChangedAt = placedAt.AddHours(i * 6)
            });
            previous = timeline[i];
        }
    }

    // Demo artwork lives in wwwroot/images/products/{sku}.svg. Products that
    // already have an image (uploaded through the admin) are left alone.
    private static async Task<int> AddDemoImagesAsync(AppDbContext db, IWebHostEnvironment environment)
    {
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var imageFolder = Path.Combine(webRoot, "images", "products");

        if (!Directory.Exists(imageFolder))
            return 0;

        var products = await db.Products
            .Where(p => !p.Images.Any())
            .Select(p => new { p.Id, p.Sku, p.Name })
            .ToListAsync();

        var added = 0;
        foreach (var product in products)
        {
            var fileName = $"{product.Sku.ToLowerInvariant()}.svg";
            if (!File.Exists(Path.Combine(imageFolder, fileName)))
                continue;

            db.ProductImages.Add(new ProductImage
            {
                ProductId = product.Id,
                Url = $"/images/products/{fileName}",
                AltText = product.Name,
                IsMain = true
            });
            added++;
        }

        return added;
    }

    private static void SeedUsers(AppDbContext db, IConfiguration config)
    {
        var section = config.GetSection("Seed");
        var hasher = new PasswordHasher<User>();

        var admin = new User
        {
            Email = section["AdminEmail"] ?? "admin@shopsphere.local",
            FirstName = "Store",
            LastName = "Admin",
            Role = UserRole.Admin
        };
        admin.PasswordHash = hasher.HashPassword(admin, RequiredSetting(section, "AdminPassword"));

        var customer = new User
        {
            Email = section["CustomerEmail"] ?? "demo@shopsphere.local",
            FirstName = "Sarah",
            LastName = "Mitchell",
            PhoneNumber = "555-0142",
            Role = UserRole.Customer
        };
        customer.PasswordHash = hasher.HashPassword(customer, RequiredSetting(section, "CustomerPassword"));
        customer.Addresses.Add(new Address
        {
            FullName = "Sarah Mitchell",
            Line1 = "742 Maple Avenue",
            City = "Portland",
            State = "OR",
            PostalCode = "97205",
            Country = "United States",
            PhoneNumber = "555-0142",
            IsDefault = true
        });

        db.Users.AddRange(admin, customer);
    }

    private static string RequiredSetting(IConfigurationSection section, string key) =>
        section[key] ?? throw new InvalidOperationException($"Missing configuration value Seed:{key}");

    private static void SeedCatalog(AppDbContext db)
    {
        var tree = new Dictionary<string, string[]>
        {
            ["Kitchen"] = ["Cookware", "Kitchen Storage", "Tableware"],
            ["Furniture"] = ["Living Room", "Home Office", "Bedroom"],
            ["Home Decor"] = ["Lighting", "Rugs", "Wall Decor"],
            ["Bath"] = ["Towels", "Bath Accessories"]
        };

        var categories = new Dictionary<string, Category>();
        var rootOrder = 0;
        foreach (var (rootName, childNames) in tree)
        {
            var root = new Category { Name = rootName, Slug = SlugHelper.Generate(rootName), SortOrder = rootOrder++ };
            for (var i = 0; i < childNames.Length; i++)
            {
                var child = new Category { Name = childNames[i], Slug = SlugHelper.Generate(childNames[i]), SortOrder = i };
                root.Children.Add(child);
                categories[child.Slug] = child;
            }
            db.Categories.Add(root);
        }

        var products = new List<Product>
        {
            NewProduct("cookware", "Cast Iron Skillet 12 inch", "CW-SKL-012", 44.99m, null, 35,
                "Pre-seasoned cast iron skillet that goes from stovetop to oven. Holds heat evenly for searing and baking."),
            NewProduct("cookware", "Stainless Steel Saucepan 2 Qt", "CW-SAU-002", 39.00m, 32.00m, 20,
                "Tri-ply stainless saucepan with a tight-fitting lid and stay-cool handle. Induction compatible."),
            NewProduct("cookware", "Nonstick Frying Pan Set (2 pc)", "CW-FRY-SET", 59.99m, 49.99m, 3,
                "8 and 10 inch ceramic nonstick pans. PFOA-free coating, dishwasher safe."),

            NewProduct("kitchen-storage", "Glass Food Storage Containers (10 pc)", "KS-GLS-010", 34.99m, null, 50,
                "Borosilicate glass containers with snap-lock lids. Safe for microwave, oven and freezer."),
            NewProduct("kitchen-storage", "Bamboo Spice Rack", "KS-SPR-001", 27.50m, null, 12,
                "Three-tier expandable spice rack made from sustainable bamboo. Fits cabinets and countertops."),
            NewProduct("kitchen-storage", "Airtight Pantry Canister Set", "KS-CAN-004", 42.00m, 36.00m, 0,
                "Set of four clear canisters with airtight lids for flour, sugar, pasta and coffee."),

            NewProduct("tableware", "Stoneware Dinner Plates (Set of 4)", "TW-PLT-004", 48.00m, null, 25,
                "Reactive-glaze stoneware plates, 10.5 inch. Each plate has a slightly different finish."),
            NewProduct("tableware", "Double-Wall Glass Mugs (Set of 2)", "TW-MUG-002", 22.99m, null, 40,
                "Insulated glass mugs that keep drinks hot and stay cool to the touch. 12 oz each."),
            NewProduct("tableware", "Acacia Wood Serving Board", "TW-BRD-001", 31.00m, 26.00m, 18,
                "Solid acacia board with a handle, great for cheese, bread and charcuterie."),

            NewProduct("living-room", "Linen Blend Three-Seat Sofa", "LR-SOF-003", 899.00m, 799.00m, 4,
                "Deep-seat sofa with removable linen blend covers and a kiln-dried hardwood frame."),
            NewProduct("living-room", "Mid-Century Coffee Table", "LR-TBL-001", 249.00m, null, 9,
                "Walnut-finish coffee table with tapered legs and a lower shelf for storage."),
            NewProduct("living-room", "Velvet Accent Chair", "LR-CHR-001", 329.00m, null, 6,
                "Channel-tufted velvet chair with brass-tipped legs. Available in emerald green."),

            NewProduct("home-office", "Height Adjustable Standing Desk", "HO-DSK-001", 459.00m, 419.00m, 7,
                "Dual-motor standing desk with memory presets. 60 x 30 inch desktop."),
            NewProduct("home-office", "Ergonomic Mesh Office Chair", "HO-CHR-001", 289.00m, null, 15,
                "Breathable mesh back, adjustable lumbar support, 4D armrests and seat depth adjustment."),
            NewProduct("home-office", "Oak Floating Wall Shelf", "HO-SHF-001", 39.99m, null, 30,
                "Solid oak shelf with hidden mounting bracket. Holds up to 40 lbs."),

            NewProduct("bedroom", "Upholstered Queen Bed Frame", "BR-BED-Q01", 649.00m, null, 5,
                "Padded headboard in performance fabric with a solid wood slat system. No box spring needed."),
            NewProduct("bedroom", "Two-Drawer Nightstand", "BR-NST-001", 139.00m, 119.00m, 11,
                "Compact nightstand with soft-close drawers and a built-in cable cutout."),
            NewProduct("bedroom", "Cotton Percale Sheet Set (Queen)", "BR-SHT-Q01", 89.00m, null, 2,
                "Crisp, breathable 100% cotton percale. Includes flat sheet, fitted sheet and two pillowcases."),

            NewProduct("lighting", "Ceramic Table Lamp", "LT-TBL-001", 74.00m, null, 22,
                "Textured ceramic base with a linen drum shade. Uses a standard E26 bulb."),
            NewProduct("lighting", "Arc Floor Lamp", "LT-FLR-001", 159.00m, 129.00m, 8,
                "Brushed brass arc lamp with a marble base. Reaches over sofas and reading chairs."),
            NewProduct("lighting", "Rattan Pendant Light", "LT-PND-001", 118.00m, null, 10,
                "Hand-woven rattan shade that casts a warm, patterned light. Adjustable cord length."),

            NewProduct("rugs", "Hand-Woven Jute Rug 5x8", "RG-JUT-58", 189.00m, null, 6,
                "Natural jute rug with a chunky braided texture. Works well in high-traffic areas."),
            NewProduct("rugs", "Washable Runner Rug 2x7", "RG-RUN-27", 69.00m, 59.00m, 14,
                "Machine-washable runner with a non-slip backing. Ideal for hallways and kitchens."),
            NewProduct("rugs", "Shag Area Rug 8x10", "RG-SHG-810", 279.00m, null, 3,
                "Plush high-pile rug in ivory. Soft underfoot for living rooms and bedrooms."),

            NewProduct("wall-decor", "Round Wall Mirror 30 inch", "WD-MIR-030", 129.00m, null, 9,
                "Thin metal frame mirror in matte black. Hangs from a single D-ring."),
            NewProduct("wall-decor", "Framed Botanical Prints (Set of 3)", "WD-PRT-003", 79.00m, 64.00m, 20,
                "Vintage-style botanical illustrations in oak frames, 11 x 14 inch each."),
            NewProduct("wall-decor", "Vintage Wall Clock", "WD-CLK-001", 58.00m, null, 12,
                "Distressed metal clock with Roman numerals and a silent sweep movement.", isActive: false),

            NewProduct("towels", "Turkish Cotton Bath Towel Set (6 pc)", "TL-BTH-006", 64.00m, null, 45,
                "Two bath towels, two hand towels and two washcloths in absorbent Turkish cotton."),
            NewProduct("towels", "Waffle Weave Hand Towels (Set of 2)", "TL-HND-002", 24.00m, 19.00m, 60,
                "Lightweight, quick-drying waffle weave towels that get softer with every wash."),

            NewProduct("bath-accessories", "Bamboo Bath Caddy", "BA-CAD-001", 36.00m, null, 16,
                "Extendable bathtub tray with a book stand, wine glass holder and phone slot."),
            NewProduct("bath-accessories", "Ceramic Soap Dispenser", "BA-SOP-001", 18.50m, null, 4,
                "Matte ceramic dispenser with a rust-proof stainless pump. Holds 12 oz."),
            NewProduct("bath-accessories", "Teak Shower Bench", "BA-BEN-001", 149.00m, null, 7,
                "Water-resistant teak bench for showers and bathrooms. Supports up to 300 lbs.")
        };

        Product NewProduct(string categorySlug, string name, string sku, decimal price, decimal? discountPrice,
            int stock, string description, bool isActive = true)
        {
            return new Product
            {
                Category = categories[categorySlug],
                Name = name,
                Slug = SlugHelper.Generate(name),
                Sku = sku,
                Description = description,
                Price = price,
                DiscountPrice = discountPrice,
                StockQuantity = stock,
                IsActive = isActive
            };
        }

        db.Products.AddRange(products);

        foreach (var product in products.Where(p => p.StockQuantity > 0))
        {
            db.InventoryMovements.Add(new InventoryMovement
            {
                Product = product,
                QuantityChange = product.StockQuantity,
                QuantityAfter = product.StockQuantity,
                Reason = InventoryChangeReason.Restock,
                Note = "Opening stock"
            });
        }
    }

    private static void SeedCoupons(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        db.Coupons.AddRange(
            new Coupon
            {
                Code = "WELCOME10",
                Description = "10% off your first order over $50",
                DiscountType = DiscountType.Percentage,
                DiscountValue = 10,
                MinOrderAmount = 50,
                MaxDiscountAmount = 100,
                UsageLimitPerCustomer = 1
            },
            new Coupon
            {
                Code = "SAVE20",
                Description = "$20 off orders over $150",
                DiscountType = DiscountType.FixedAmount,
                DiscountValue = 20,
                MinOrderAmount = 150
            },
            new Coupon
            {
                Code = "FLASH50",
                Description = "$50 off orders over $300, first 5 customers only",
                DiscountType = DiscountType.FixedAmount,
                DiscountValue = 50,
                MinOrderAmount = 300,
                UsageLimit = 5,
                ExpiresAt = now.AddDays(30)
            },
            new Coupon
            {
                Code = "SUMMER25",
                Description = "Summer sale, 25% off",
                DiscountType = DiscountType.Percentage,
                DiscountValue = 25,
                StartsAt = now.AddMonths(-3),
                ExpiresAt = now.AddMonths(-1)
            });
    }
}
