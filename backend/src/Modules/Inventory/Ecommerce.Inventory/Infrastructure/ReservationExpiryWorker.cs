using Ecommerce.Inventory.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Inventory.Infrastructure;

/// <summary>Runs the expiry sweep on a timer, one new scope per tick. A failed tick is logged and the next one still runs.</summary>
internal sealed class ReservationExpiryWorker(
    IServiceScopeFactory scopes,
    IOptions<InventoryOptions> options,
    ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(options.Value.ReservationSweepSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SweepOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task SweepOnceAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = scopes.CreateScope();
            int expired = await scope.ServiceProvider.GetRequiredService<ReservationExpiryService>().SweepAsync(ct);
            if (expired > 0)
            {
                logger.LogInformation("Expired {Count} stock reservations.", expired);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Reservation expiry sweep failed; it will run again on the next tick.");
        }
    }
}
