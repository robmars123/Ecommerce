using Ecommerce.SharedKernel.Projections;
using Ecommerce.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ecommerce.Persistence.Projections;

/// <summary>
/// Replay (arch 8.8.2, Slice 6b D5): under the projector's advisory lock, one transaction resets its read model, its dedupe marks,
/// its versions and its checkpoint. The outbox is retained, so the next passes rebuild everything from it.
/// </summary>
internal sealed class ProjectionReplayer(
    EcommerceDbContext db,
    IEnumerable<IProjector> projectors,
    IClock clock,
    Microsoft.Extensions.Options.IOptions<DatabaseOptions> database) : IProjectionReplayer
{
    public async Task<ReplayOutcome> ReplayAsync(string name, CancellationToken ct)
    {
        IProjector? projector = projectors.FirstOrDefault(p => p.Name == name);
        if (projector is null)
        {
            return ReplayOutcome.UnknownProjector;
        }

        string key = "projection:" + name;
        await using NpgsqlConnection lockConnection = new(database.Value.ConnectionString);
        await lockConnection.OpenAsync(ct);
        if (!await AdvisoryLock.TryLockAsync(lockConnection, key, ct))
        {
            return ReplayOutcome.Busy;
        }

        try
        {
            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
            await projector.ResetAsync(ct);
            await db.Database.ExecuteSqlAsync($"DELETE FROM processed_events WHERE consumer = {name}", ct);
            await db.Database.ExecuteSqlAsync($"DELETE FROM projection_versions WHERE projector = {name}", ct);
            DateTimeOffset now = clock.UtcNow;
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO consumer_checkpoints (consumer, last_sequence, updated_at) VALUES ({name}, 0, {now})
                ON CONFLICT (consumer) DO UPDATE SET last_sequence = 0, updated_at = EXCLUDED.updated_at
                """, ct);
            await tx.CommitAsync(ct);
            return ReplayOutcome.Done;
        }
        finally
        {
            await AdvisoryLock.UnlockQuietlyAsync(lockConnection, key);
        }
    }
}

/// <summary>Lag, as-of and dead letters (arch 8.8.3, Slice 6b D6, D7). Lag is measured from the oldest event the projector has not finished.</summary>
internal sealed class ConsumerHealthReader(
    EcommerceDbContext db,
    IEnumerable<IProjector> projectors,
    IClock clock,
    IOptions<ProjectionsOptions> options) : IConsumerHealthReader
{
    public async Task<IReadOnlyList<ProjectorHealth>> GetProjectorsAsync(CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        List<ProjectorHealth> result = [];
        foreach (IProjector projector in projectors.OrderBy(p => p.Name))
        {
            string name = projector.Name;
            string[] types = [.. projector.EventTypes];
            long checkpoint = (await db.Database.SqlQuery<long>($"SELECT last_sequence AS \"Value\" FROM consumer_checkpoints WHERE consumer = {name}").ToListAsync(ct)).SingleOrDefault();
            List<DateTimeOffset> oldest = await db.Database.SqlQuery<DateTimeOffset>($"""
                SELECT e.occurred_at AS "Value"
                FROM outbox_events e
                LEFT JOIN processed_events p ON p.consumer = {name} AND p.event_id = e.event_id
                WHERE e.sequence > {checkpoint} AND e.event_type = ANY({types})
                  AND (p.event_id IS NULL OR p.status = 'Retrying')
                ORDER BY e.occurred_at
                LIMIT 1
                """).ToListAsync(ct);
            int deadLetters = (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM processed_events WHERE consumer = {name} AND status = 'DeadLetter'").ToListAsync(ct)).Single();
            DateTimeOffset asOf = oldest.Count == 0 ? now : oldest[0];
            int lag = oldest.Count == 0 ? 0 : (int)Math.Max(0, (now - asOf).TotalSeconds);
            result.Add(new ProjectorHealth(name, checkpoint, lag, asOf, deadLetters, lag > options.Value.LagTargetSeconds));
        }
        return result;
    }

    public async Task<IReadOnlyList<ConsumerDeadLetters>> GetConsumerDeadLettersAsync(CancellationToken ct)
    {
        string[] projectorNames = [.. projectors.Select(p => p.Name)];
        return await db.Database.SqlQuery<ConsumerDeadLetters>($"""
            SELECT consumer AS "Consumer", count(*)::int AS "Count" FROM processed_events
            WHERE status = 'DeadLetter' AND NOT (consumer = ANY({projectorNames}))
            GROUP BY consumer ORDER BY consumer
            """).ToListAsync(ct);
    }
}

/// <summary>Logs ProjectionLagAlert (4102, Warning) for every projector over the lag target and returns them. Quiet when all are within it.</summary>
public sealed class ProjectionLagMonitor(IConsumerHealthReader health, ILogger<ProjectionLagMonitor> logger)
{
    public static readonly EventId LagAlertEvent = new(4102, "ProjectionLagAlert");

    public async Task<IReadOnlyList<ProjectorHealth>> CheckAsync(CancellationToken ct)
    {
        List<ProjectorHealth> lagging = (await health.GetProjectorsAsync(ct)).Where(h => h.Lagging).ToList();
        foreach (ProjectorHealth h in lagging)
        {
            logger.LogWarning(LagAlertEvent, "Projector {Projector} is {LagSeconds} seconds behind (as of {AsOf})", h.Name, h.LagSeconds, h.AsOf);
        }
        return lagging;
    }
}

/// <summary>
/// Polls every registered <see cref="IProjector"/>, one new scope per tick, then checks lag. A failed tick is logged and the next
/// one still runs. <c>Projections:Worker:Enabled=false</c> turns it off (tests drive <see cref="ProjectionProcessor"/> directly).
/// </summary>
internal sealed class ProjectionWorker(
    IServiceScopeFactory scopes, IConfiguration configuration, IOptions<ProjectionsOptions> options, ILogger<ProjectionWorker> logger) : BackgroundService
{
    private const int MaxBatchesPerTick = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(configuration["Projections:Worker:Enabled"], "false", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(options.Value.PollIntervalMs));
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
            ProjectionProcessor processor = scope.ServiceProvider.GetRequiredService<ProjectionProcessor>();
            foreach (IProjector projector in scope.ServiceProvider.GetServices<IProjector>())
            {
                for (int batch = 0; batch < MaxBatchesPerTick && await processor.RunOnceAsync(projector, ct) == ProjectionProcessor.BatchSize; batch++)
                {
                }
            }
            await scope.ServiceProvider.GetRequiredService<ProjectionLagMonitor>().CheckAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Projection tick failed; it will run again on the next tick.");
        }
    }
}
