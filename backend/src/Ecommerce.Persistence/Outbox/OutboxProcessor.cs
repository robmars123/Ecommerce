using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ecommerce.Persistence.Outbox;

/// <summary>Retry spacing (arch 8.4): 2^attempts seconds, at most five minutes; more than <see cref="MaxAttempts"/> attempts is a dead letter.</summary>
public static class OutboxBackoff
{
    public const int MaxAttempts = 5;
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    public static TimeSpan DelayAfter(int attempts) =>
        attempts >= 9 ? MaxDelay : TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempts), MaxDelay.TotalSeconds));
}

/// <summary>
/// Drives one <see cref="IOutboxConsumer"/> over the outbox (arch 8.4, Slice 6 D1 to D4). Events of the consumer's types with no
/// terminal <c>processed_events</c> row (and a due retry, if a row exists) are handled in sequence order, batch of 50.
/// A session-level advisory lock on a dedicated connection keeps one worker per consumer; it is not a transaction, so the
/// handler (which may call a provider) runs with none open (rule 6). A dead letter never touches order state.
/// </summary>
public sealed class OutboxProcessor(
    EcommerceDbContext db,
    IClock clock,
    IOptions<DatabaseOptions> database,
    ILogger<OutboxProcessor> logger)
{
    public const int BatchSize = 50;
    public static readonly EventId DeadLetterEvent = new(4101, "OutboxDeadLetter");

    /// <summary>Handles one batch. Returns how many events were handled (successfully or not); 0 when another worker holds the consumer.</summary>
    public async Task<int> RunOnceAsync(IOutboxConsumer consumer, CancellationToken ct)
    {
        await using NpgsqlConnection lockConnection = new(database.Value.ConnectionString);
        await lockConnection.OpenAsync(ct);
        if (!await TryLockAsync(lockConnection, consumer.Name, ct))
        {
            return 0;
        }

        try
        {
            return await HandleBatchAsync(consumer, ct);
        }
        finally
        {
            await UnlockQuietlyAsync(lockConnection, consumer.Name);
        }
    }

    private async Task<int> HandleBatchAsync(IOutboxConsumer consumer, CancellationToken ct)
    {
        string[] types = [.. consumer.EventTypes];
        DateTimeOffset now = clock.UtcNow;
        string name = consumer.Name;
        List<OutboxEventEntry> due = await db.Set<OutboxEventEntry>()
            .FromSql($"""
                SELECT e.sequence, e.event_id, e.event_type, e.aggregate_type, e.aggregate_id, e.aggregate_version, e.payload, e.occurred_at
                FROM outbox_events e
                LEFT JOIN processed_events p ON p.consumer = {name} AND p.event_id = e.event_id
                WHERE e.event_type = ANY({types})
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
                await consumer.HandleAsync(message, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                await RecordFailureAsync(name, entry.EventId, made, ex, ct);
                continue;
            }
            await UpsertAsync(name, entry.EventId, ProcessedEventStatus.Done, made, null, null, ct);
        }
        return due.Count;
    }

    private async Task RecordFailureAsync(string consumer, Guid eventId, int attempts, Exception ex, CancellationToken ct)
    {
        // Type and message only: no stack, no payload. Consumers never put secrets in exception messages.
        string error = $"{ex.GetType().Name}: {ex.Message}";
        error = error.Length > 500 ? error[..500] : error;
        if (attempts > OutboxBackoff.MaxAttempts)
        {
            await UpsertAsync(consumer, eventId, ProcessedEventStatus.DeadLetter, attempts, null, error, ct);
            logger.LogCritical(
                DeadLetterEvent, "Outbox event {OutboxEventId} dead-lettered for consumer {Consumer} after {Attempts} attempts: {Error}",
                eventId, consumer, attempts, error);
            return;
        }
        await UpsertAsync(consumer, eventId, ProcessedEventStatus.Retrying, attempts, clock.UtcNow + OutboxBackoff.DelayAfter(attempts), error, ct);
        logger.LogWarning("Outbox event {OutboxEventId} failed for consumer {Consumer} (attempt {Attempts}); will retry", eventId, consumer, attempts);
    }

    private async Task UpsertAsync(
        string consumer, Guid eventId, string status, int attempts, DateTimeOffset? nextAttemptAt, string? error, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        // One statement is one short transaction of its own.
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO processed_events (consumer, event_id, status, attempts, next_attempt_at, last_error, processed_at)
            VALUES ({consumer}, {eventId}, {status}, {attempts}, {nextAttemptAt}::timestamptz, {error}::text, {now})
            ON CONFLICT (consumer, event_id) DO UPDATE SET
                status = EXCLUDED.status, attempts = EXCLUDED.attempts, next_attempt_at = EXCLUDED.next_attempt_at,
                last_error = EXCLUDED.last_error, processed_at = EXCLUDED.processed_at
            """, ct);
    }

    private static async Task<bool> TryLockAsync(NpgsqlConnection connection, string consumer, CancellationToken ct)
    {
        await using NpgsqlCommand cmd = new("SELECT pg_try_advisory_lock(hashtext(@key))", connection);
        cmd.Parameters.AddWithValue("key", "outbox:" + consumer);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static async Task UnlockQuietlyAsync(NpgsqlConnection connection, string consumer)
    {
        try
        {
            await using NpgsqlCommand cmd = new("SELECT pg_advisory_unlock(hashtext(@key))", connection);
            cmd.Parameters.AddWithValue("key", "outbox:" + consumer);
            await cmd.ExecuteScalarAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // A broken connection drops its session locks anyway.
        }
    }
}
