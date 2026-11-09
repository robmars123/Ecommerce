using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Orders;

namespace Ecommerce.Api.IntegrationTests.Refunds;

// Test 16 (arch 11.2) and the unfulfillable endpoint (Slice 6b D17, D18): a fulfillment that fails finally owes exactly one refund.
public class FulfillmentSagaTests(PostgresFixture postgres)
{
    private const string Reason = "Supplier cannot deliver this item";
    private const string Consumer = "payments.fulfillment-compensation";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Guid SagaIdOf(IdentityTestEnv.Api r) => r.Json.GetProperty("sagaId").GetGuid();

    private static Task<IdentityTestEnv.Api> UnfulfillableAsync(RefundEnv e, Guid order, string? token = null, object? body = null) =>
        e.Env.PostAsync($"/api/v1/admin/orders/{order}/unfulfillable", body ?? new { reason = Reason }, token ?? e.Admin);

    // ---- Test 16 ----

    [Fact]
    public async Task T16_a_double_submit_and_a_repeated_consumer_and_a_duplicate_webhook_make_one_saga_one_refund_one_provider_refund_and_a_Refunded_order()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();

        IdentityTestEnv.Api first = await UnfulfillableAsync(e, order);
        IdentityTestEnv.Api second = await UnfulfillableAsync(e, order);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Guid saga = SagaIdOf(first);
        Assert.Equal(saga, SagaIdOf(second));
        Assert.Equal("Compensating", first.Json.GetProperty("status").GetString());
        Assert.Equal("25.00", first.Json.GetProperty("refundAmount").GetProperty("amount").GetString());
        Assert.Equal(1, await e.F.CountAsync("SELECT count(*) FROM fulfillment_sagas WHERE order_id = @o", ("o", order)));
        Assert.Equal(1, await e.F.OutboxCountAsync("FulfillmentFailed"));

        // No shipment between the mark and the refund.
        IdentityTestEnv.Api ship = await e.OrderActionAsync(order, "ship", new { carrierCode = OrdersEnv.Carrier, trackingNumber = "TRK-1" });
        Assert.Equal(HttpStatusCode.Conflict, ship.Status);
        Assert.Contains("order-unfulfillable", ship.Text);

        // The consumer runs, then "crashes before its mark" (the processed row is lost) and runs again.
        await e.Env.DrainOutboxAsync(rounds: 3);
        await e.F.Cart.ExecAsync("DELETE FROM processed_events WHERE consumer = @c", ("c", Consumer));
        await e.Env.DrainOutboxAsync(rounds: 3);

        Assert.Equal(1, await e.F.CountAsync("SELECT count(*) FROM refunds r JOIN payments p ON p.id = r.payment_id WHERE p.order_id = @o", ("o", order)));
        Guid refund = await e.F.ScalarAsync<Guid>("SELECT r.id FROM refunds r JOIN payments p ON p.id = r.payment_id WHERE p.order_id = @o", ("o", order));
        Assert.Equal($"saga:{saga}:Fulfill", await e.F.ScalarAsync<string>("SELECT idempotency_key FROM refunds WHERE id = @r", ("r", refund)));
        Assert.Equal(25.00m, await e.F.ScalarAsync<decimal>("SELECT amount FROM refunds WHERE id = @r", ("r", refund)));
        Assert.Equal(1, e.F.Gateway.DistinctRefunds);
        Assert.All(e.F.Gateway.RefundCalls, c => Assert.Equal($"re:{refund}", c.IdempotencyKey));
        Assert.Equal(0, await e.F.CountAsync("SELECT count(*) FROM processed_events WHERE status = 'DeadLetter'"));

        await e.RefundWebhookAsync(refund, "succeeded", eventId: "evt_saga_1");
        await e.RefundWebhookAsync(refund, "succeeded", eventId: "evt_saga_1");
        await e.RefundWebhookAsync(refund, "succeeded", eventId: "evt_saga_2");

        Assert.Equal("Refunded", await e.F.OrderStatusAsync(order));
        Assert.Equal(1, await e.HistoryCountAsync(order, "Refunded"));
        Assert.Equal("Compensated", await e.F.ScalarAsync<string>("SELECT status FROM fulfillment_sagas WHERE id = @s", ("s", saga)));
        Assert.Equal(1, await e.F.CountAsync("SELECT count(*) FROM refunds"));

        // A repeat after compensation still answers with the same saga and adds no event.
        IdentityTestEnv.Api again = await UnfulfillableAsync(e, order);
        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.Equal(saga, SagaIdOf(again));
        Assert.Equal("Compensated", again.Json.GetProperty("status").GetString());
        Assert.Equal(1, await e.F.OutboxCountAsync("FulfillmentFailed"));
    }

    [Fact]
    public async Task T16_the_audit_row_has_the_actor_the_before_and_after_state_and_the_reason()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync("Processing");
        Guid saga = SagaIdOf(await UnfulfillableAsync(e, order));

        // A Processing order could be shipped; the mark alone stops it.
        IdentityTestEnv.Api ship = await e.OrderActionAsync(order, "ship", new { carrierCode = OrdersEnv.Carrier, trackingNumber = "TRK-2" });
        Assert.Equal(HttpStatusCode.Conflict, ship.Status);
        Assert.Contains("order-unfulfillable", ship.Text);
        Assert.Equal("Processing", await e.F.OrderStatusAsync(order));

        Assert.Equal(1, await e.AuditCountAsync("fulfillment.unfulfillable", order));
        Guid actor = await e.F.ScalarAsync<Guid>("SELECT actor_id FROM audit_log WHERE action = 'fulfillment.unfulfillable'");
        Assert.Equal(await e.F.ScalarAsync<Guid>("SELECT id FROM users WHERE email = 'admin@example.test'"), actor);
        Assert.Equal(Reason, await e.F.ScalarAsync<string>("SELECT reason FROM audit_log WHERE action = 'fulfillment.unfulfillable'"));
        using JsonDocument before = JsonDocument.Parse(await e.F.ScalarAsync<string>("SELECT before_json::text FROM audit_log WHERE action = 'fulfillment.unfulfillable'"));
        using JsonDocument after = JsonDocument.Parse(await e.F.ScalarAsync<string>("SELECT after_json::text FROM audit_log WHERE action = 'fulfillment.unfulfillable'"));
        Assert.Equal("Processing", before.RootElement.GetProperty("status").GetString());
        Assert.Equal(saga, after.RootElement.GetProperty("sagaId").GetGuid());
        Assert.Equal("Compensating", after.RootElement.GetProperty("sagaStatus").GetString());
    }

    [Fact]
    public async Task The_saga_refund_is_what_remains_after_an_earlier_partial_refund()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        await e.RefundWebhookAsync(RefundEnv.RefundIdOf(await e.RefundAsync(order, "10.00")), "succeeded");

        IdentityTestEnv.Api r = await UnfulfillableAsync(e, order);
        await e.Env.DrainOutboxAsync(rounds: 3);

        Assert.Equal("15.00", r.Json.GetProperty("refundAmount").GetProperty("amount").GetString());
        Assert.Equal(15.00m, await e.F.ScalarAsync<decimal>("SELECT amount FROM refunds WHERE idempotency_key LIKE 'saga:%'"));
        Assert.Equal(25.00m, await e.CommittedAsync(order));
    }

    // ---- the endpoint ----

    [Fact]
    public async Task Unfulfillable_is_401_anonymous_and_403_for_a_Customer_and_for_Support_and_creates_nothing()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await e.Env.PostAsync($"/api/v1/admin/orders/{order}/unfulfillable", new { reason = Reason })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await UnfulfillableAsync(e, order, e.TokenA)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await UnfulfillableAsync(e, order, e.Support)).Status);
        Assert.Equal(0, await e.F.CountAsync("SELECT count(*) FROM fulfillment_sagas"));
        Assert.Equal(0, await e.F.OutboxCountAsync("FulfillmentFailed"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Unfulfillable_without_a_reason_is_400(string? reason)
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();

        IdentityTestEnv.Api r = await UnfulfillableAsync(e, order, null, new { reason });

        Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        Assert.True(r.Json.GetProperty("errors").TryGetProperty("reason", out _));
        Assert.Equal(0, await e.F.CountAsync("SELECT count(*) FROM fulfillment_sagas"));
    }

    [Fact]
    public async Task Unfulfillable_with_a_501_character_reason_is_400_and_500_characters_is_accepted()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await UnfulfillableAsync(e, order, null, new { reason = new string('x', 501) })).Status);
        Assert.Equal(HttpStatusCode.OK, (await UnfulfillableAsync(e, order, null, new { reason = new string('x', 500) })).Status);
    }

    [Fact]
    public async Task Unfulfillable_for_an_unknown_order_is_404()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);

        Assert.Equal(HttpStatusCode.NotFound, (await UnfulfillableAsync(e, Guid.NewGuid())).Status);
    }

    [Theory]
    [InlineData("PendingPayment")]
    [InlineData("Shipped")]
    [InlineData("Delivered")]
    [InlineData("Cancelled")]
    [InlineData("Refunded")]
    public async Task Unfulfillable_from_a_status_other_than_Paid_or_Processing_is_409_and_creates_nothing(string status)
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync(status);

        Assert.Equal(HttpStatusCode.Conflict, (await UnfulfillableAsync(e, order)).Status);
        Assert.Equal(0, await e.F.CountAsync("SELECT count(*) FROM fulfillment_sagas"));
        Assert.Equal(0, await e.F.OutboxCountAsync("FulfillmentFailed"));
    }
}
