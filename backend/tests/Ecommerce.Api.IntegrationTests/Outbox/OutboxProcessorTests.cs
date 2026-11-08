using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Persistence;
using Ecommerce.Persistence.Outbox;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Serilog.Events;

namespace Ecommerce.Api.IntegrationTests.Outbox;

/// <summary>A consumer the tests script: what it does, what it saw.</summary>
internal sealed class ProbeConsumer(string name, params string[] eventTypes) : IOutboxConsumer
{
    private readonly List<Guid> _handled = [];
    private readonly Lock _gate = new();

    public string Name => name;

    public IReadOnlySet<string> EventTypes { get; } = new HashSet<string>(eventTypes);

    public Func<OutboxMessage, Task> Behavior { get; set; } = _ => Task.CompletedTask;

    public IReadOnlyList<Guid> Handled
    {
        get
        {
            lock (_gate)
            {
                return _handled.ToList();
            }
        }
    }

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct)
    {
        lock (_gate)
        {
            _handled.Add(message.EventId);
        }
        await Behavior(message);
    }
}

// Outbox runtime (arch 8.4, Slice 6 D1 to D4): dedupe, retry, dead letter, no transaction around the handler, one worker.
public class OutboxProcessorTests(PostgresFixture postgres)
{
    private const string EventType = "ProbeEvent";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<Guid> AddEventAsync(IdentityTestEnv env, string type = EventType)
    {
        using IServiceScope scope = env.Factory.Services.CreateScope();
        Guid id = Guid.CreateVersion7();
        scope.ServiceProvider.GetRequiredService<IOutbox>().Add(
            new OutboxMessage(id, type, "Probe", Guid.NewGuid(), 1, "{}", env.Clock.UtcNow));
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return id;
    }

    private static async Task<int> RunAsync(IdentityTestEnv env, IOutboxConsumer consumer)
    {
        using IServiceScope scope = env.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().RunOnceAsync(consumer, Ct);
    }

    private static async Task<(string Status, int Attempts, DateTimeOffset? Next, string? Error)> RowAsync(IdentityTestEnv env, string consumer, Guid eventId)
    {
        await using NpgsqlConnection connection = new(env.Factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value.ConnectionString);
        await connection.OpenAsync(Ct);
        await using NpgsqlCommand cmd = new("SELECT status, attempts, next_attempt_at, last_error FROM processed_events WHERE consumer = @c AND event_id = @e", connection);
        cmd.Parameters.AddWithValue("c", consumer);
        cmd.Parameters.AddWithValue("e", eventId);
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct), "no processed_events row");
        return (reader.GetString(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2), reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    [Fact]
    public async Task An_event_is_handled_once_per_consumer_and_marked_Done()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeConsumer probe = new("probe.a", EventType);
        Guid id = await AddEventAsync(env);

        Assert.Equal(1, await RunAsync(env, probe));
        Assert.Equal(0, await RunAsync(env, probe));

        Assert.Equal([id], probe.Handled);
        (string status, int attempts, _, _) = await RowAsync(env, "probe.a", id);
        Assert.Equal(("Done", 1), (status, attempts));
    }

    [Fact]
    public async Task Two_consumers_each_get_the_same_event_independently()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeConsumer a = new("probe.a", EventType);
        ProbeConsumer b = new("probe.b", EventType) { Behavior = _ => throw new InvalidOperationException("b is broken") };
        Guid id = await AddEventAsync(env);

        await RunAsync(env, a);
        await RunAsync(env, b);

        Assert.Equal("Done", (await RowAsync(env, "probe.a", id)).Status);
        Assert.Equal("Retrying", (await RowAsync(env, "probe.b", id)).Status);
        Assert.Equal([id], a.Handled);
    }

    [Fact]
    public async Task An_event_of_another_type_is_not_selected()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeConsumer probe = new("probe.a", EventType);
        await AddEventAsync(env, "SomethingElse");

        Assert.Equal(0, await RunAsync(env, probe));
        Assert.Empty(probe.Handled);
    }

    [Fact]
    public async Task An_event_that_commits_late_with_a_lower_sequence_is_still_handled()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeConsumer probe = new("probe.a", EventType);
        string cs = env.Factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value.ConnectionString;

        // Writer 1 takes the lower sequence number but commits last.
        await using NpgsqlConnection slow = new(cs);
        await slow.OpenAsync(Ct);
        await using NpgsqlTransaction tx = await slow.BeginTransactionAsync(Ct);
        Guid late = Guid.CreateVersion7();
        await using (NpgsqlCommand insert = new(
            "INSERT INTO outbox_events (event_id, event_type, aggregate_type, aggregate_id, aggregate_version, payload, occurred_at) VALUES (@e, @t, 'Probe', @a, 1, '{}', now())", slow, tx))
        {
            insert.Parameters.AddWithValue("e", late);
            insert.Parameters.AddWithValue("t", EventType);
            insert.Parameters.AddWithValue("a", Guid.NewGuid());
            await insert.ExecuteNonQueryAsync(Ct);
        }
        Guid early = await AddEventAsync(env); // higher sequence, commits first

        Assert.Equal(1, await RunAsync(env, probe));
        Assert.Equal([early], probe.Handled);

        await tx.CommitAsync(Ct);

        Assert.Equal(1, await RunAsync(env, probe));
        Assert.Equal([early, late], probe.Handled);
    }

    [Fact]
    public async Task A_failure_retries_with_exponential_backoff_on_the_injected_clock()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeConsumer probe = new("probe.a", EventType) { Behavior = _ => throw new InvalidOperationException("provider down") };
        Guid id = await AddEventAsync(env);
        DateTimeOffset t0 = env.Clock.UtcNow;

        Assert.Equal(1, await RunAsync(env, probe));
        (string status, int attempts, DateTimeOffset? next, string? error) = await RowAsync(env, "probe.a", id);
        Assert.Equal(("Retrying", 1), (status, attempts));
        Assert.Equal(t0.AddSeconds(2), next);
        Assert.Contains("provider down", error);

        Assert.Equal(0, await RunAsync(env, probe)); // not due yet

        env.Clock.UtcNow = t0.AddSeconds(2);
        Assert.Equal(1, await RunAsync(env, probe));
        (_, attempts, next, _) = await RowAsync(env, "probe.a", id);
        Assert.Equal(2, attempts);
        Assert.Equal(t0.AddSeconds(2).AddSeconds(4), next);
    }

    [Fact]
    public async Task More_than_five_failures_dead_letters_with_a_critical_alert_and_stops_retrying()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeConsumer probe = new("probe.a", EventType) { Behavior = _ => throw new InvalidOperationException("provider down") };
        Guid id = await AddEventAsync(env);

        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(1, await RunAsync(env, probe));
            env.Clock.UtcNow = env.Clock.UtcNow.AddMinutes(10);
        }

        (string status, int attempts, DateTimeOffset? next, _) = await RowAsync(env, "probe.a", id);
        Assert.Equal(("DeadLetter", 6, null), (status, attempts, next));
        Assert.Equal(0, await RunAsync(env, probe));
        Assert.Equal(6, probe.Handled.Count);
        Assert.Contains(env.Logs.Events, e => e.Level == LogEventLevel.Fatal && e.RenderMessage().Contains("dead-lettered") && e.RenderMessage().Contains(id.ToString()));
    }

    [Fact]
    public async Task The_handler_runs_with_no_database_transaction_open()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        using IServiceScope scope = env.Factory.Services.CreateScope();
        EcommerceDbContext db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
        bool transactionOpen = true;
        ProbeConsumer probe = new("probe.a", EventType) { Behavior = _ => { transactionOpen = db.Database.CurrentTransaction is not null; return Task.CompletedTask; } };
        await AddEventAsync(env);

        await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().RunOnceAsync(probe, Ct);

        Assert.Single(probe.Handled);
        Assert.False(transactionOpen, "the outbox handler ran inside a database transaction (rule 6)");
    }

    [Fact]
    public async Task Two_processors_racing_one_consumer_handle_the_event_once()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        ProbeConsumer probe = new("probe.a", EventType) { Behavior = _ => Task.Delay(700, Ct) };
        await AddEventAsync(env);

        int[] handled = await Task.WhenAll(RunAsync(env, probe), RunAsync(env, probe));

        Assert.Equal(1, handled.Sum());
        Assert.Single(probe.Handled);
    }
}
