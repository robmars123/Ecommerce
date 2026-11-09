using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Persistence;
using Ecommerce.Persistence.Projections;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Serilog.Events;

namespace Ecommerce.Api.IntegrationTests.Projections;

/// <summary>A projector the tests script: it records what the runtime handed it and in which order.</summary>
internal sealed class ProbeProjector(string name, string[] eventTypes, string[]? versioned = null) : IProjector
{
    private readonly List<OutboxMessage> _applied = [];
    private readonly Lock _gate = new();

    public string Name => name;

    public IReadOnlySet<string> EventTypes { get; } = new HashSet<string>(eventTypes);

    public IReadOnlySet<string> VersionedAggregateTypes { get; } = new HashSet<string>(versioned ?? []);

    public IReadOnlyList<OutboxMessage> Applied
    {
        get
        {
            lock (_gate)
            {
                return _applied.ToList();
            }
        }
    }

    public Task ApplyAsync(OutboxMessage message, CancellationToken ct)
    {
        lock (_gate)
        {
            _applied.Add(message);
        }
        return Task.CompletedTask;
    }

    public Task ResetAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            _applied.Clear();
        }
        return Task.CompletedTask;
    }
}

internal static class ProjectionTestSupport
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string ConnectionString(IdentityTestEnv env) =>
        env.Factory.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString;

    public static async Task<OutboxMessage> AddEventAsync(
        IdentityTestEnv env, string type, Guid? aggregateId = null, int version = 1, string aggregateType = "Probe", DateTimeOffset? at = null, string payload = "{}")
    {
        using IServiceScope scope = env.Factory.Services.CreateScope();
        OutboxMessage message = new(Guid.CreateVersion7(), type, aggregateType, aggregateId ?? Guid.NewGuid(), version, payload, at ?? env.Clock.UtcNow);
        scope.ServiceProvider.GetRequiredService<IOutbox>().Add(message);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return message;
    }

    public static async Task<int> RunAsync(IdentityTestEnv env, IProjector projector)
    {
        using IServiceScope scope = env.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ProjectionProcessor>().RunOnceAsync(projector, Ct);
    }

    public static async Task<T> ScalarAsync<T>(IdentityTestEnv env, string sql, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(ConnectionString(env));
        await using NpgsqlCommand cmd = source.CreateCommand(sql);
        foreach ((string name, object value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        object? scalar = await cmd.ExecuteScalarAsync(Ct);
        return scalar is null or DBNull ? default! : (T)scalar;
    }

    public static Task<long> CheckpointAsync(IdentityTestEnv env, string name) =>
        ScalarAsync<long>(env, "SELECT COALESCE((SELECT last_sequence FROM consumer_checkpoints WHERE consumer = @c), 0)", ("c", name));

    public static Task<long> SequenceOfAsync(IdentityTestEnv env, Guid eventId) =>
        ScalarAsync<long>(env, "SELECT sequence FROM outbox_events WHERE event_id = @e", ("e", eventId));

    public static Task<long> MaxSequenceAsync(IdentityTestEnv env) =>
        ScalarAsync<long>(env, "SELECT COALESCE(max(sequence), 0) FROM outbox_events");

    public static Task<string> MarkAsync(IdentityTestEnv env, string consumer, Guid eventId) =>
        ScalarAsync<string>(env, "SELECT status FROM processed_events WHERE consumer = @c AND event_id = @e", ("c", consumer), ("e", eventId));
}

// Projection runtime (arch 8.8.2, Slice 6b D1 to D4): tests 11 and 14, the sequence-gap rule and the checkpoint.
public class ProjectionRuntimeTests(PostgresFixture postgres)
{
    private const string EventType = "ProbeEvent";
    private static CancellationToken Ct => ProjectionTestSupport.Ct;

    [Fact]
    public async Task T11_runtime_the_same_event_delivered_twice_is_applied_once_by_the_pass_and_by_a_direct_re_apply()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeProjector projector = new("probe.proj", [EventType]);
        OutboxMessage evt = await ProjectionTestSupport.AddEventAsync(env, EventType);

        Assert.Equal(1, await ProjectionTestSupport.RunAsync(env, projector));
        Assert.Equal(0, await ProjectionTestSupport.RunAsync(env, projector));
        using IServiceScope scope = env.Factory.Services.CreateScope();
        bool applied = await scope.ServiceProvider.GetRequiredService<ProjectionProcessor>().ApplyAsync(projector, evt, 1, Ct);

        Assert.False(applied);
        Assert.Equal([evt.EventId], projector.Applied.Select(m => m.EventId));
        Assert.Equal("Done", await ProjectionTestSupport.MarkAsync(env, "probe.proj", evt.EventId));
    }

    [Fact]
    public async Task T13_runtime_a_projector_that_did_not_run_catches_up_exactly_and_a_second_pass_changes_nothing()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeProjector projector = new("probe.proj", [EventType]);
        List<Guid> ids = [];
        for (int i = 0; i < 5; i++)
        {
            ids.Add((await ProjectionTestSupport.AddEventAsync(env, EventType)).EventId);
        }
        env.Clock.UtcNow = env.Clock.UtcNow.AddMinutes(10);

        Assert.Equal(5, await ProjectionTestSupport.RunAsync(env, projector));

        Assert.Equal(ids, projector.Applied.Select(m => m.EventId));
        long max = await ProjectionTestSupport.MaxSequenceAsync(env);
        Assert.Equal(max, await ProjectionTestSupport.CheckpointAsync(env, "probe.proj"));
        Assert.Equal(0, await ProjectionTestSupport.RunAsync(env, projector));
        Assert.Equal(5, projector.Applied.Count);
        Assert.Equal(max, await ProjectionTestSupport.CheckpointAsync(env, "probe.proj"));
    }

    [Fact]
    public async Task Gap_commit_a_lower_sequence_that_commits_late_is_applied_and_the_checkpoint_never_passes_a_row_younger_than_the_window()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeProjector projector = new("probe.proj", [EventType]);

        // Writer A draws the lower sequence and stays open; B commits first.
        await using NpgsqlConnection slow = new(ProjectionTestSupport.ConnectionString(env));
        await slow.OpenAsync(Ct);
        await using NpgsqlTransaction tx = await slow.BeginTransactionAsync(Ct);
        Guid late = Guid.CreateVersion7();
        await using (NpgsqlCommand insert = new(
            "INSERT INTO outbox_events (event_id, event_type, aggregate_type, aggregate_id, aggregate_version, payload, occurred_at) VALUES (@e, @t, 'Probe', @a, 1, '{}', @at)", slow, tx))
        {
            insert.Parameters.AddWithValue("e", late);
            insert.Parameters.AddWithValue("t", EventType);
            insert.Parameters.AddWithValue("a", Guid.NewGuid());
            insert.Parameters.AddWithValue("at", env.Clock.UtcNow);
            await insert.ExecuteNonQueryAsync(Ct);
        }
        OutboxMessage early = await ProjectionTestSupport.AddEventAsync(env, EventType);

        Assert.Equal(1, await ProjectionTestSupport.RunAsync(env, projector));
        Assert.Equal([early.EventId], projector.Applied.Select(m => m.EventId));
        long earlySequence = await ProjectionTestSupport.SequenceOfAsync(env, early.EventId);
        Assert.True(await ProjectionTestSupport.CheckpointAsync(env, "probe.proj") < earlySequence - 1, "checkpoint passed the uncommitted lower sequence");

        await tx.CommitAsync(Ct);
        long lateSequence = await ProjectionTestSupport.SequenceOfAsync(env, late);

        Assert.Equal(1, await ProjectionTestSupport.RunAsync(env, projector));
        Assert.Equal([early.EventId, late], projector.Applied.Select(m => m.EventId));
        Assert.True(lateSequence < earlySequence);
        Assert.Equal(0, await ProjectionTestSupport.CheckpointAsync(env, "probe.proj"));

        env.Clock.UtcNow = env.Clock.UtcNow.AddSeconds(31);
        await ProjectionTestSupport.RunAsync(env, projector);
        Assert.Equal(earlySequence, await ProjectionTestSupport.CheckpointAsync(env, "probe.proj"));
    }

    [Fact]
    public async Task The_checkpoint_does_not_pass_a_row_younger_than_the_safety_window_until_the_window_has_elapsed()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeProjector projector = new("probe.proj", [EventType]);
        await ProjectionTestSupport.AddEventAsync(env, EventType);

        Assert.Equal(1, await ProjectionTestSupport.RunAsync(env, projector));
        Assert.Equal(0, await ProjectionTestSupport.CheckpointAsync(env, "probe.proj"));

        env.Clock.UtcNow = env.Clock.UtcNow.AddSeconds(31);
        await ProjectionTestSupport.RunAsync(env, projector);
        Assert.Equal(await ProjectionTestSupport.MaxSequenceAsync(env), await ProjectionTestSupport.CheckpointAsync(env, "probe.proj"));
    }

    [Fact]
    public async Task T14_versions_arriving_as_1_3_2_hold_3_until_2_is_applied_then_apply_it_after_the_backoff_and_skip_a_stale_version()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeProjector projector = new("probe.proj", [EventType], ["Probe"]);
        Guid order = Guid.NewGuid();
        DateTimeOffset t0 = env.Clock.UtcNow;
        OutboxMessage v1 = await ProjectionTestSupport.AddEventAsync(env, EventType, order, 1);
        OutboxMessage v3 = await ProjectionTestSupport.AddEventAsync(env, EventType, order, 3);
        OutboxMessage v2 = await ProjectionTestSupport.AddEventAsync(env, EventType, order, 2);

        Assert.Equal(3, await ProjectionTestSupport.RunAsync(env, projector));

        Assert.Equal([1, 2], projector.Applied.Select(m => m.AggregateVersion));
        Assert.Equal("Retrying", await ProjectionTestSupport.MarkAsync(env, "probe.proj", v3.EventId));

        env.Clock.UtcNow = t0.AddSeconds(3);
        Assert.Equal(1, await ProjectionTestSupport.RunAsync(env, projector));
        Assert.Equal([1, 2, 3], projector.Applied.Select(m => m.AggregateVersion));
        Assert.Equal("Done", await ProjectionTestSupport.MarkAsync(env, "probe.proj", v3.EventId));

        OutboxMessage stale = await ProjectionTestSupport.AddEventAsync(env, EventType, order, 2);
        await ProjectionTestSupport.RunAsync(env, projector);
        Assert.Equal(3, projector.Applied.Count);
        Assert.Equal("Done", await ProjectionTestSupport.MarkAsync(env, "probe.proj", stale.EventId));
        Assert.Equal("Done", await ProjectionTestSupport.MarkAsync(env, "probe.proj", v1.EventId));
        Assert.Equal("Done", await ProjectionTestSupport.MarkAsync(env, "probe.proj", v2.EventId));
    }

    [Fact]
    public async Task T14_a_missing_version_dead_letters_the_later_one_after_the_attempts_with_a_critical_alert_and_applies_nothing_past_it()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeProjector projector = new("probe.proj", [EventType], ["Probe"]);
        Guid order = Guid.NewGuid();
        await ProjectionTestSupport.AddEventAsync(env, EventType, order, 1);
        OutboxMessage v3 = await ProjectionTestSupport.AddEventAsync(env, EventType, order, 3);

        for (int i = 0; i < 6; i++)
        {
            await ProjectionTestSupport.RunAsync(env, projector);
            env.Clock.UtcNow = env.Clock.UtcNow.AddMinutes(10);
        }

        Assert.Equal("DeadLetter", await ProjectionTestSupport.MarkAsync(env, "probe.proj", v3.EventId));
        Assert.Equal([1], projector.Applied.Select(m => m.AggregateVersion));
        Assert.Contains(env.Logs.Events, e => e.Level == LogEventLevel.Fatal && e.RenderMessage().Contains("dead-lettered") && e.RenderMessage().Contains(v3.EventId.ToString()));
    }
}
