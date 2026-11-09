using Ecommerce.Persistence.Outbox;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Projections;
using Ecommerce.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ecommerce.Persistence.Projections;

/// <summary>The aggregate's version is more than one ahead of the projection's: apply nothing, retry later (arch 8.8.2).</summary>
internal sealed class ProjectionVersionGapException(string message) : Exception(message);

/// <summary>
/// Drives one <see cref="IProjector"/> over the outbox (arch 8.8, Slice 6b D1 to D4). Per event ONE transaction holds the dedupe
/// mark, the version gate, the projector's writes and the version move, so a duplicate is a no-op. Events are selected above the
/// projector's checkpoint (sequence order, batch 50). The checkpoint only advances over rows that are terminal for this projector
/// and older than the safety window, so a lower sequence that commits late is still found.
/// </summary>
public sealed class ProjectionProcessor(
    EcommerceDbContext db,
    IClock clock,
    IOptions<DatabaseOptions> database,
    IOptions<ProjectionsOptions> options,
    ILogger<ProjectionProcessor> logger)
{
    public const int BatchSize = 50;

    /// <summary>Handles one batch. Returns how many events were handled (applied, skipped or failed); 0 when another worker holds the projector.</summary>
    public async Task<int> RunOnceAsync(IProjector projector, CancellationToken ct)
    {
        string key = "projection:" + projector.Name;
        await using NpgsqlConnection lockConnection = new(database.Value.ConnectionString);
        await lockConnection.OpenAsync(ct);
        if (!await AdvisoryLock.TryLockAsync(lockConnection, key, ct))
        {
            return 0;
        }

        try
        {
            int handled = await HandleBatchAsync(projector, ct);
            await AdvanceCheckpointAsync(projector, ct);
            return handled;
        }
        finally
        {
            await AdvisoryLock.UnlockQuietlyAsync(lockConnection, key);
        }
    }

    private async Task<int> HandleBatchAsync(IProjector projector, CancellationToken ct)
    {
        string name = projector.Name;
        string[] types = [.. projector.EventTypes];
        DateTimeOffset now = clock.UtcNow;
        long checkpoint = await CheckpointAsync(name, ct);
        List<OutboxEventEntry> due = await db.Set<OutboxEventEntry>()
            .FromSql($"""
                SELECT e.sequence, e.event_id, e.event_type, e.aggregate_type, e.aggregate_id, e.aggregate_version, e.payload, e.occurred_at
                FROM outbox_events e
                LEFT JOIN processed_events p ON p.consumer = {name} AND p.event_id = e.event_id
                WHERE e.sequence > {checkpoint}
                  AND e.event_type = ANY({types})
                  AND (p.event_id IS NULL OR (p.status = 'Retrying' AND p.next_attempt_at <= {now}))
                ORDER BY e.sequence
                LIMIT {BatchSize}
                """)
            .AsNoTracking()
            .ToListAsync(ct);
        if (due.Count == 0)
        {
            return 0;
        }

        Guid[] ids = [.. due.Select(e => e.EventId)];
        Dictionary<Guid, int> attempts = await db.Set<ProcessedEventEntry>().AsNoTracking()
            .Where(p => p.Consumer == name && ids.Contains(p.EventId))
            .ToDictionaryAsync(p => p.EventId, p => p.Attempts, ct);

        foreach (OutboxEventEntry entry in due)
        {
            int made = attempts.GetValueOrDefault(entry.EventId) + 1;
            OutboxMessage message = new(
                entry.EventId, entry.EventType, entry.AggregateType, entry.AggregateId, entry.AggregateVersion, entry.Payload, entry.OccurredAt);
            try
            {
                await ApplyAsync(projector, message, made, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                await RecordFailureAsync(name, entry.EventId, made, ex, ct);
            }
        }
        return due.Count;
    }

    /// <summary>
    /// One event in one transaction. False when the event was already done or its version is stale (no-op). Throws
    /// <see cref="ProjectionVersionGapException"/> (nothing written) when the aggregate's earlier version has not arrived.
    /// </summary>
    public async Task<bool> ApplyAsync(IProjector projector, OutboxMessage message, int attempts, CancellationToken ct)
    {
        string name = projector.Name;
        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            DateTimeOffset now = clock.UtcNow;
            int marked = await db.Database.ExecuteSqlAsync($"""
                INSERT INTO processed_events (consumer, event_id, status, attempts, next_attempt_at, last_error, processed_at)
                VALUES ({name}, {message.EventId}, {ProcessedEventStatus.Done}, {attempts}, NULL, NULL, {now})
                ON CONFLICT (consumer, event_id) DO UPDATE SET
                    status = EXCLUDED.status, attempts = EXCLUDED.attempts, next_attempt_at = NULL, last_error = NULL, processed_at = EXCLUDED.processed_at
                WHERE processed_events.status <> 'Done'
                """, ct);
            if (marked == 0)
            {
                await tx.CommitAsync(ct);
                return false;
            }

            bool versioned = projector.VersionedAggregateTypes.Contains(message.AggregateType);
            if (versioned)
            {
                int stored = (await db.Database.SqlQuery<int>($"""
                    SELECT version AS "Value" FROM projection_versions
                    WHERE projector = {name} AND aggregate_type = {message.AggregateType} AND aggregate_id = {message.AggregateId}
                    """).ToListAsync(ct)).SingleOrDefault();
                if (message.AggregateVersion <= stored)
                {
                    await tx.CommitAsync(ct);
                    return false;
                }
                if (message.AggregateVersion > stored + 1)
                {
                    throw new ProjectionVersionGapException(
                        $"{message.AggregateType} {message.AggregateId} is at version {stored}; version {message.AggregateVersion} arrived first.");
                }
            }

            await projector.ApplyAsync(message, ct);
            await db.SaveChangesAsync(ct);
            if (versioned)
            {
                await db.Database.ExecuteSqlAsync($"""
                    INSERT INTO projection_versions (projector, aggregate_type, aggregate_id, version)
                    VALUES ({name}, {message.AggregateType}, {message.AggregateId}, {message.AggregateVersion})
                    ON CONFLICT (projector, aggregate_type, aggregate_id) DO UPDATE SET version = EXCLUDED.version
                    """, ct);
            }
            await tx.CommitAsync(ct);
            db.ChangeTracker.Clear();
            return true;
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(CancellationToken.None);
            }
            catch (Exception)
            {
                // The connection may already be broken; the original exception is the one that matters.
            }
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task RecordFailureAsync(string projector, Guid eventId, int attempts, Exception ex, CancellationToken ct)
    {
        string error = $"{ex.GetType().Name}: {ex.Message}";
        error = error.Length > 500 ? error[..500] : error;
        bool dead = attempts > OutboxBackoff.MaxAttempts;
        DateTimeOffset now = clock.UtcNow;
        string status = dead ? ProcessedEventStatus.DeadLetter : ProcessedEventStatus.Retrying;
        DateTimeOffset? next = dead ? null : now + OutboxBackoff.DelayAfter(attempts);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO processed_events (consumer, event_id, status, attempts, next_attempt_at, last_error, processed_at)
            VALUES ({projector}, {eventId}, {status}, {attempts}, {next}::timestamptz, {error}::text, {now})
            ON CONFLICT (consumer, event_id) DO UPDATE SET
                status = EXCLUDED.status, attempts = EXCLUDED.attempts, next_attempt_at = EXCLUDED.next_attempt_at,
                last_error = EXCLUDED.last_error, processed_at = EXCLUDED.processed_at
            """, ct);
        if (dead)
        {
            logger.LogCritical(
                OutboxProcessor.DeadLetterEvent, "Outbox event {OutboxEventId} dead-lettered for projector {Consumer} after {Attempts} attempts: {Error}",
                eventId, projector, attempts, error);
            return;
        }
        logger.LogWarning("Outbox event {OutboxEventId} failed for projector {Consumer} (attempt {Attempts}); will retry", eventId, projector, attempts);
    }

    private async Task<long> CheckpointAsync(string name, CancellationToken ct) =>
        (await db.Database.SqlQuery<long>($"SELECT last_sequence AS \"Value\" FROM consumer_checkpoints WHERE consumer = {name}").ToListAsync(ct)).SingleOrDefault();

    /// <summary>
    /// Arch 8.8.2 and Slice 6b D3: the checkpoint moves to just below the first blocking row, or to the highest visible sequence when
    /// nothing blocks. A row blocks while it is younger than the safety window, or is one of the projector's types and not terminal.
    /// </summary>
    private async Task AdvanceCheckpointAsync(IProjector projector, CancellationToken ct)
    {
        string name = projector.Name;
        string[] types = [.. projector.EventTypes];
        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset cutoff = now - TimeSpan.FromSeconds(options.Value.SafetyWindowSeconds);
        long checkpoint = await CheckpointAsync(name, ct);
        long blocker = (await db.Database.SqlQuery<long>($"""
            SELECT COALESCE(min(e.sequence), 0) AS "Value"
            FROM outbox_events e
            LEFT JOIN processed_events p ON p.consumer = {name} AND p.event_id = e.event_id
            WHERE e.sequence > {checkpoint}
              AND (e.occurred_at > {cutoff}
                   OR (e.event_type = ANY({types}) AND (p.event_id IS NULL OR p.status = 'Retrying')))
            """).ToListAsync(ct)).Single();
        long target = (await db.Database.SqlQuery<long>($"""
            SELECT COALESCE(max(sequence), 0) AS "Value" FROM outbox_events
            WHERE sequence > {checkpoint} AND ({blocker} = 0 OR sequence < {blocker})
            """).ToListAsync(ct)).Single();
        if (target <= checkpoint)
        {
            return;
        }

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO consumer_checkpoints (consumer, last_sequence, updated_at) VALUES ({name}, {target}, {now})
            ON CONFLICT (consumer) DO UPDATE SET
                last_sequence = GREATEST(consumer_checkpoints.last_sequence, EXCLUDED.last_sequence), updated_at = EXCLUDED.updated_at
            """, ct);
    }
}
