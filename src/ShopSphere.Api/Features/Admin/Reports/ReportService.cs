using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Data;

namespace ShopSphere.Api.Features.Admin.Reports;

public class ReportRange : IValidatableObject
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    [RegularExpression("^(day|month)$")]
    public string GroupBy { get; set; } = "day";

    [Range(1, 100)]
    public int Top { get; set; } = 10;

    public DateTime FromDate => (From ?? DateTime.UtcNow.AddDays(-29)).Date;

    // The end date is inclusive, so compare with the start of the next day
    public DateTime ToExclusive => (To ?? DateTime.UtcNow).Date.AddDays(1);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From.HasValue && To.HasValue && To < From)
            yield return new ValidationResult("The end date must be after the start date.", new[] { nameof(To) });
    }
}

public class SalesByPeriodDto
{
    public string Period { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
    public int Units { get; set; }
}

public class SalesByProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
}

public class SalesByCategoryDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
}

public class OrderStatusSummaryDto
{
    public string Status { get; set; } = string.Empty;
    public int Orders { get; set; }
    public decimal Total { get; set; }
}

public record PeriodAggregate(DateOnly Period, decimal Revenue, int Orders);

public class ReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<SalesByPeriodDto>> GetSalesByDateAsync(ReportRange range)
    {
        var orders = _db.Orders
            .AsNoTracking()
            .CountedAsRevenue()
            .Where(o => o.PlacedAt >= range.FromDate && o.PlacedAt < range.ToExclusive);

        var items = _db.OrderItems
            .AsNoTracking()
            .FromRevenueOrders()
            .Where(i => i.Order.PlacedAt >= range.FromDate && i.Order.PlacedAt < range.ToExclusive);

        var byMonth = range.GroupBy == "month";

        // Revenue comes from order totals (shipping and discount included) while
        // units come from the order lines, so they are two grouped queries that
        // get merged on the period key.
        var orderTotals = byMonth
            ? await orders
                .GroupBy(o => new { o.PlacedAt.Year, o.PlacedAt.Month })
                .Select(g => new PeriodAggregate(new DateOnly(g.Key.Year, g.Key.Month, 1), g.Sum(o => o.Total), g.Count()))
                .ToListAsync()
            : await orders
                .GroupBy(o => o.PlacedAt.Date)
                .Select(g => new PeriodAggregate(DateOnly.FromDateTime(g.Key), g.Sum(o => o.Total), g.Count()))
                .ToListAsync();

        var unitsByPeriod = byMonth
            ? await items
                .GroupBy(i => new { i.Order.PlacedAt.Year, i.Order.PlacedAt.Month })
                .Select(g => new { Period = new DateOnly(g.Key.Year, g.Key.Month, 1), Units = g.Sum(i => i.Quantity) })
                .ToDictionaryAsync(x => x.Period, x => x.Units)
            : await items
                .GroupBy(i => i.Order.PlacedAt.Date)
                .Select(g => new { Period = DateOnly.FromDateTime(g.Key), Units = g.Sum(i => i.Quantity) })
                .ToDictionaryAsync(x => x.Period, x => x.Units);

        return orderTotals
            .OrderBy(x => x.Period)
            .Select(x => new SalesByPeriodDto
            {
                PeriodStart = x.Period,
                Period = x.Period.ToString(byMonth ? "MMM yyyy" : "dd MMM"),
                Revenue = x.Revenue,
                Orders = x.Orders,
                Units = unitsByPeriod.GetValueOrDefault(x.Period)
            })
            .ToList();
    }

    public Task<List<SalesByProductDto>> GetSalesByProductAsync(ReportRange range)
    {
        return _db.OrderItems
            .AsNoTracking()
            .FromRevenueOrders()
            .Where(i => i.Order.PlacedAt >= range.FromDate && i.Order.PlacedAt < range.ToExclusive)
            .GroupBy(i => new { i.ProductId, i.ProductName, i.Sku })
            .Select(g => new SalesByProductDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                Sku = g.Key.Sku,
                UnitsSold = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.LineTotal)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(range.Top)
            .ToListAsync();
    }

    public Task<List<SalesByCategoryDto>> GetSalesByCategoryAsync(ReportRange range)
    {
        return _db.OrderItems
            .AsNoTracking()
            .FromRevenueOrders()
            .Where(i => i.Order.PlacedAt >= range.FromDate && i.Order.PlacedAt < range.ToExclusive)
            .GroupBy(i => new { i.Product.CategoryId, CategoryName = i.Product.Category.Name })
            .Select(g => new SalesByCategoryDto
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                UnitsSold = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.LineTotal)
            })
            .OrderByDescending(x => x.Revenue)
            .ToListAsync();
    }

    // Includes cancelled and pending orders on purpose: this report is about
    // where orders end up, not about revenue
    public Task<List<OrderStatusSummaryDto>> GetOrderStatusSummaryAsync(ReportRange range)
    {
        return _db.Orders
            .AsNoTracking()
            .Where(o => o.PlacedAt >= range.FromDate && o.PlacedAt < range.ToExclusive)
            .GroupBy(o => o.Status)
            .Select(g => new OrderStatusSummaryDto
            {
                Status = g.Key.ToString(),
                Orders = g.Count(),
                Total = g.Sum(o => o.Total)
            })
            .OrderByDescending(x => x.Orders)
            .ToListAsync();
    }
}
