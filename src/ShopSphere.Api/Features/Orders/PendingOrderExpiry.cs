using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Orders;

public class OrderSettings
{
    public const string SectionName = "Orders";

    // How long an unpaid order keeps its stock reserved
    public int PendingTimeoutMinutes { get; set; } = 30;

    public int ExpiryCheckIntervalMinutes { get; set; } = 5;
}

// Cancels orders that were placed but never paid, so their stock goes back
// on sale. Kept separate from the hosted service so it can be tested directly.
public class PendingOrderExpiry
{
    private const int BatchSize = 50;

    private readonly AppDbContext _db;
    private readonly OrderWorkflow _workflow;
    private readonly OrderSettings _settings;
    private readonly ILogger<PendingOrderExpiry> _logger;

    public PendingOrderExpiry(AppDbContext db, OrderWorkflow workflow, IOptions<OrderSettings> settings, ILogger<PendingOrderExpiry> logger)
    {
        _db = db;
        _workflow = workflow;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<int> ExpireAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-_settings.PendingTimeoutMinutes);

        var orders = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o => o.Status == OrderStatus.Pending && o.PlacedAt < cutoff)
            .OrderBy(o => o.PlacedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
            return 0;

        foreach (var order in orders)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            await _workflow.ChangeStatusAsync(order, OrderStatus.Cancelled, changedByUserId: null,
                note: "Cancelled automatically, payment was not completed",
                restoreReason: InventoryChangeReason.OrderExpired);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        _logger.LogInformation("Cancelled {Count} unpaid orders older than {Minutes} minutes",
            orders.Count, _settings.PendingTimeoutMinutes);

        return orders.Count;
    }
}
