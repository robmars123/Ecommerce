using Ecommerce.SharedKernel.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Persistence.Outbox;

/// <summary>
/// Polls every registered <see cref="IOutboxConsumer"/> on a timer, one new scope per tick. A failed tick is logged and the next
/// one still runs. <c>Outbox:Worker:Enabled=false</c> turns it off (tests drive <see cref="OutboxProcessor"/> directly).
/// </summary>
internal sealed class OutboxWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<OutboxWorker> logger) : BackgroundService
{
    private const int MaxBatchesPerTick = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(configuration["Outbox:Worker:Enabled"], "false", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using PeriodicTimer timer = new(TimeSpan.FromSeconds(Math.Max(1, int.TryParse(configuration["Outbox:Worker:PollSeconds"], out int poll) ? poll : 2)));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await TickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = scopes.CreateScope();
            OutboxProcessor processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
            foreach (IOutboxConsumer consumer in scope.ServiceProvider.GetServices<IOutboxConsumer>())
            {
                for (int batch = 0; batch < MaxBatchesPerTick && await processor.RunOnceAsync(consumer, ct) == OutboxProcessor.BatchSize; batch++)
                {
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Outbox tick failed; it will run again on the next tick.");
        }
    }
}
