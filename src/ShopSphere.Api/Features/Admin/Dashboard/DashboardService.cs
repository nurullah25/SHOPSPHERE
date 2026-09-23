using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Admin.Dashboard;

public class DashboardService
{
    private const int RecentOrderCount = 8;
    private const int LowStockCount = 8;

    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardDto> GetDashboardAsync(int days)
    {
        var now = DateTime.UtcNow;
        var periodStart = now.Date.AddDays(-(days - 1));
        var previousStart = periodStart.AddDays(-days);

        var revenueOrders = _db.Orders.AsNoTracking().CountedAsRevenue();

        var currentPeriod = await revenueOrders
            .Where(o => o.PlacedAt >= periodStart)
            .GroupBy(o => 1)
            .Select(g => new { Revenue = g.Sum(o => o.Total), Orders = g.Count() })
            .SingleOrDefaultAsync();

        var previousPeriod = await revenueOrders
            .Where(o => o.PlacedAt >= previousStart && o.PlacedAt < periodStart)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;

        var dailyRevenue = await revenueOrders
            .Where(o => o.PlacedAt >= periodStart)
            .GroupBy(o => o.PlacedAt.Date)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(o => o.Total), Orders = g.Count() })
            .ToListAsync();

        var byDate = dailyRevenue.ToDictionary(x => DateOnly.FromDateTime(x.Date));

        return new DashboardDto
        {
            PeriodDays = days,
            RevenueInPeriod = currentPeriod?.Revenue ?? 0m,
            RevenuePreviousPeriod = previousPeriod,
            OrdersInPeriod = currentPeriod?.Orders ?? 0,
            AverageOrderValue = currentPeriod is { Orders: > 0 }
                ? Math.Round(currentPeriod.Revenue / currentPeriod.Orders, 2)
                : 0m,
            NewCustomersInPeriod = await _db.Users.CountAsync(u => u.Role == UserRole.Customer && u.CreatedAt >= periodStart),

            RevenueAllTime = await revenueOrders.SumAsync(o => (decimal?)o.Total) ?? 0m,
            TotalOrders = await _db.Orders.CountAsync(),
            TotalCustomers = await _db.Users.CountAsync(u => u.Role == UserRole.Customer),
            ActiveProducts = await _db.Products.CountAsync(p => p.IsActive),

            PendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending),
            LowStockProducts = await _db.Products.CountAsync(p => p.IsActive && p.StockQuantity <= p.LowStockThreshold),

            // Days without orders still need a point so the chart has no gaps
            RevenueByDay = Enumerable.Range(0, days)
                .Select(offset => DateOnly.FromDateTime(periodStart.AddDays(offset)))
                .Select(date => new DailyRevenueDto
                {
                    Date = date,
                    Revenue = byDate.GetValueOrDefault(date)?.Revenue ?? 0m,
                    Orders = byDate.GetValueOrDefault(date)?.Orders ?? 0
                })
                .ToList(),

            RecentOrders = await _db.Orders
                .AsNoTracking()
                .OrderByDescending(o => o.PlacedAt).ThenByDescending(o => o.Id)
                .Take(RecentOrderCount)
                .Select(o => new RecentOrderDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    CustomerName = o.User.FirstName + " " + o.User.LastName,
                    Status = o.Status.ToString(),
                    Total = o.Total,
                    PlacedAt = o.PlacedAt
                })
                .ToListAsync(),

            LowStock = await _db.Products
                .AsNoTracking()
                .Where(p => p.IsActive && p.StockQuantity <= p.LowStockThreshold)
                .OrderBy(p => p.StockQuantity)
                .Take(LowStockCount)
                .Select(p => new LowStockProductDto
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    Sku = p.Sku,
                    StockQuantity = p.StockQuantity,
                    LowStockThreshold = p.LowStockThreshold
                })
                .ToListAsync()
        };
    }
}
