using Microsoft.Extensions.Options;

namespace ShopSphere.Api.Features.Orders;

public class PendingOrderExpiryJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrderSettings _settings;
    private readonly ILogger<PendingOrderExpiryJob> _logger;

    public PendingOrderExpiryJob(IServiceScopeFactory scopeFactory, IOptions<OrderSettings> settings, ILogger<PendingOrderExpiryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_settings.ExpiryCheckIntervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // The expiry service is scoped because it uses the DbContext
                using var scope = _scopeFactory.CreateScope();
                var expiry = scope.ServiceProvider.GetRequiredService<PendingOrderExpiry>();
                await expiry.ExpireAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed run shouldn't stop the job, the next tick tries again
                _logger.LogError(ex, "Failed to expire unpaid orders");
            }
        }
    }
}
