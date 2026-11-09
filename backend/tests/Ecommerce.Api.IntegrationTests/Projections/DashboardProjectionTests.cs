using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Persistence.Projections;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Projections;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;

namespace Ecommerce.Api.IntegrationTests.Projections;

// Tests 11, 13 and 18 (arch 11.2) against the real dashboard projector (Slice 6b D8, D9).
public class DashboardProjectionTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonElement Kpis(IdentityTestEnv.Api dashboard) => dashboard.Json.GetProperty("kpis");

    private static int StatusCount(IdentityTestEnv.Api dashboard, string status) =>
        dashboard.Json.GetProperty("ordersByStatus").EnumerateArray()
            .Where(s => s.GetProperty("status").GetString() == status)
            .Select(s => s.GetProperty("count").GetInt32()).SingleOrDefault();

    [Fact]
    public async Task T11_the_same_OrderPaid_delivered_twice_counts_revenue_and_orders_once_and_so_does_one_RefundSucceeded()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        Guid order = await e.PaidOrderAsync("DASH-11");
        decimal total = await e.TotalOfAsync(order);
        Guid refund = RefundIdOf(await e.R.RefundAsync(order, "5.00"));
        await e.R.RefundWebhookAsync(refund, "succeeded");
        await e.DrainAsync();

        // A second delivery of each event: the next pass, then a direct re-apply of the stored message.
        await e.DrainAsync();
        using (IServiceScope scope = e.Env.Factory.Services.CreateScope())
        {
            ProjectionProcessor processor = scope.ServiceProvider.GetRequiredService<ProjectionProcessor>();
            IProjector projector = scope.ServiceProvider.GetServices<IProjector>().Single(p => p.Name == "admin.dashboard");
            foreach (string type in new[] { "OrderPaid", "RefundSucceeded" })
            {
                OutboxMessage message = await ReadAsync(e, type);
                Assert.False(await processor.ApplyAsync(projector, message, 1, Ct), $"{type} was applied a second time");
            }
        }

        IdentityTestEnv.Api dashboard = await e.OkDashboardAsync();
        Assert.Equal(total - 5.00m, ProjectionEnv.AmountOf(Kpis(dashboard).GetProperty("revenue")));
        Assert.Equal(1, Kpis(dashboard).GetProperty("orderCount").GetInt32());
    }

    private static Guid RefundIdOf(IdentityTestEnv.Api accepted) => accepted.Json.GetProperty("refundId").GetGuid();

    private static async Task<OutboxMessage> ReadAsync(ProjectionEnv e, string type)
    {
        Guid id = await e.F.ScalarAsync<Guid>("SELECT event_id FROM outbox_events WHERE event_type = @t", ("t", type));
        string aggregateType = await e.F.ScalarAsync<string>("SELECT aggregate_type FROM outbox_events WHERE event_id = @i", ("i", id));
        Guid aggregateId = await e.F.ScalarAsync<Guid>("SELECT aggregate_id FROM outbox_events WHERE event_id = @i", ("i", id));
        int version = await e.F.ScalarAsync<int>("SELECT aggregate_version FROM outbox_events WHERE event_id = @i", ("i", id));
        string payload = await e.F.ScalarAsync<string>("SELECT payload::text FROM outbox_events WHERE event_id = @i", ("i", id));
        DateTimeOffset at = await e.F.ScalarAsync<DateTimeOffset>("SELECT occurred_at FROM outbox_events WHERE event_id = @i", ("i", id));
        return new OutboxMessage(id, type, aggregateType, aggregateId, version, payload, at);
    }

    [Fact]
    public async Task T13_a_projector_that_did_not_run_while_orders_were_paid_and_refunded_catches_up_exactly_with_the_lag_alert_raised_then_cleared()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        Guid one = await e.PaidOrderAsync("DASH-13A");
        Guid two = await e.PaidOrderAsync("DASH-13B");
        Guid three = await e.PaidOrderAsync("DASH-13C");
        decimal oneTotal = await e.TotalOfAsync(one);
        decimal gross = oneTotal + await e.TotalOfAsync(two) + await e.TotalOfAsync(three);
        await e.R.RefundWebhookAsync(RefundIdOf(await e.R.RefundAsync(one, ProjectionEnv.Money(oneTotal))), "succeeded"); // full: one -> Refunded
        await e.R.RefundWebhookAsync(RefundIdOf(await e.R.RefundAsync(two, "5.00")), "succeeded"); // partial: two stays Paid
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddMinutes(10);

        using (IServiceScope scope = e.Env.Factory.Services.CreateScope())
        {
            IReadOnlyList<ProjectorHealth> lagging = await scope.ServiceProvider.GetRequiredService<ProjectionLagMonitor>().CheckAsync(Ct);
            Assert.Contains(lagging, h => h.Name == "admin.dashboard" && h.LagSeconds >= 600);
        }
        Assert.Contains(e.Env.Logs.Events, l => l.Level == LogEventLevel.Warning && l.RenderMessage().Contains("admin.dashboard") && l.RenderMessage().Contains("behind"));

        await e.DrainAsync();

        IdentityTestEnv.Api dashboard = await e.OkDashboardAsync();
        Assert.Equal(gross - oneTotal - 5.00m, ProjectionEnv.AmountOf(Kpis(dashboard).GetProperty("revenue")));
        Assert.Equal(3, Kpis(dashboard).GetProperty("orderCount").GetInt32());
        Assert.Equal((2, 1), (StatusCount(dashboard, "Paid"), StatusCount(dashboard, "Refunded")));
        long max = await e.F.ScalarAsync<long>("SELECT max(sequence) FROM outbox_events");
        Assert.Equal(max, await e.F.ScalarAsync<long>("SELECT last_sequence FROM consumer_checkpoints WHERE consumer = 'admin.dashboard'"));
        using (IServiceScope scope = e.Env.Factory.Services.CreateScope())
        {
            Assert.DoesNotContain(await scope.ServiceProvider.GetRequiredService<ProjectionLagMonitor>().CheckAsync(Ct), h => h.Name == "admin.dashboard");
        }

        await e.DrainAsync();
        IdentityTestEnv.Api again = await e.OkDashboardAsync();
        Assert.Equal(dashboard.Json.GetProperty("kpis").GetRawText(), again.Json.GetProperty("kpis").GetRawText());
        Assert.Equal(dashboard.Json.GetProperty("ordersByStatus").GetRawText(), again.Json.GetProperty("ordersByStatus").GetRawText());

        // Arch 8.8.2: the checkpoint never passes a row younger than the safety window, however far the projector got.
        await e.PaidOrderAsync("DASH-13D");
        await e.DrainAsync();
        long youngest = await e.F.ScalarAsync<long>("SELECT max(sequence) FROM outbox_events");
        Assert.True(await e.F.ScalarAsync<long>("SELECT last_sequence FROM consumer_checkpoints WHERE consumer = 'admin.dashboard'") < youngest);
        Assert.Equal(4, Kpis(await e.OkDashboardAsync()).GetProperty("orderCount").GetInt32());
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddSeconds(31);
        await e.DrainAsync();
        Assert.Equal(youngest, await e.F.ScalarAsync<long>("SELECT last_sequence FROM consumer_checkpoints WHERE consumer = 'admin.dashboard'"));
    }
}
