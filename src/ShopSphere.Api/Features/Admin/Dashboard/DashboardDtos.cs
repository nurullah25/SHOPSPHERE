namespace ShopSphere.Api.Features.Admin.Dashboard;

public class DashboardDto
{
    public int PeriodDays { get; set; }

    public decimal RevenueInPeriod { get; set; }
    public decimal RevenuePreviousPeriod { get; set; }
    public int OrdersInPeriod { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int NewCustomersInPeriod { get; set; }

    public decimal RevenueAllTime { get; set; }
    public int TotalOrders { get; set; }
    public int TotalCustomers { get; set; }
    public int ActiveProducts { get; set; }

    // Things that need someone to look at them
    public int PendingOrders { get; set; }
    public int LowStockProducts { get; set; }

    public List<DailyRevenueDto> RevenueByDay { get; set; } = new();
    public List<RecentOrderDto> RecentOrders { get; set; } = new();
    public List<LowStockProductDto> LowStock { get; set; } = new();
}

public class DailyRevenueDto
{
    public DateOnly Date { get; set; }
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
}

public class RecentOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime PlacedAt { get; set; }
}

public class LowStockProductDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; }
}
