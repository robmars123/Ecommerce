using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;

namespace Ecommerce.Api.IntegrationTests.Refunds;

// RefundSucceeded (Slice 6b D12): the revenue-reducing event. Only a refund an actor asked for emits it.
public class RefundSucceededEventTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_admin_refund_that_succeeds_emits_one_RefundSucceeded_with_its_amount_even_when_the_webhook_repeats()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        Guid refund = RefundEnv.RefundIdOf(await e.RefundAsync(order, "10.00"));

        await e.RefundWebhookAsync(refund, "succeeded", eventId: "evt_rs_1");
        await e.RefundWebhookAsync(refund, "succeeded", eventId: "evt_rs_1");
        await e.RefundWebhookAsync(refund, "succeeded", eventId: "evt_rs_2");

        Assert.Equal(1, await e.F.OutboxCountAsync("RefundSucceeded"));
        string payload = await e.F.ScalarAsync<string>("SELECT payload::text FROM outbox_events WHERE event_type = 'RefundSucceeded'");
        using JsonDocument json = JsonDocument.Parse(payload);
        Assert.Equal(refund, json.RootElement.GetProperty("refundId").GetGuid());
        Assert.Equal(order, json.RootElement.GetProperty("orderId").GetGuid());
        Assert.Equal(10.00m, json.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal(("Refund", refund, 2), (
            await e.F.ScalarAsync<string>("SELECT aggregate_type FROM outbox_events WHERE event_type = 'RefundSucceeded'"),
            await e.F.ScalarAsync<Guid>("SELECT aggregate_id FROM outbox_events WHERE event_type = 'RefundSucceeded'"),
            await e.F.ScalarAsync<int>("SELECT aggregate_version FROM outbox_events WHERE event_type = 'RefundSucceeded'")));
    }

    [Fact]
    public async Task A_failed_admin_refund_emits_nothing()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        Guid refund = RefundEnv.RefundIdOf(await e.RefundAsync(order, "10.00"));

        await e.RefundWebhookAsync(refund, "failed");

        Assert.Equal(0, await e.F.OutboxCountAsync("RefundSucceeded"));
    }

    [Fact]
    public async Task An_automatic_refund_that_succeeds_emits_nothing_because_it_returns_money_that_was_never_revenue()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid variant = await e.F.Cart.VariantAsync("RS-AUTO", stock: 10);
        Guid orderId = await e.F.PlaceOrderAsync(variant, 1, "rs-auto-" + Guid.NewGuid().ToString("N"));
        string intent = await e.F.IntentOfAsync(orderId);
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddMinutes(16);
        Assert.Equal(1, await e.F.SweepAsync());
        Assert.Equal(System.Net.HttpStatusCode.OK, (await e.F.WebhookAsync("evt_rs_late", intent)).Status);
        Guid auto = await e.F.ScalarAsync<Guid>("SELECT id FROM refunds WHERE actor_id IS NULL");

        await e.RefundWebhookAsync(auto, "succeeded");

        Assert.Equal("Succeeded", await e.RefundStatusAsync(auto));
        Assert.Equal(0, await e.F.OutboxCountAsync("RefundSucceeded"));
    }
}
