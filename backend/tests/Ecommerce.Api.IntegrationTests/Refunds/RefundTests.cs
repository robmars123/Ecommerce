using System.Net;
using Ecommerce.Api.IntegrationTests.Identity;

namespace Ecommerce.Api.IntegrationTests.Refunds;

// Tests 26, 27, 28 (arch 11.2) and the refund endpoint's own 401/403/400/404 (Test 7 refund part). Captured amount: 25.00 USD.
public class RefundTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- Test 26: the ceiling ----

    [Fact]
    public async Task T26_parallel_refunds_never_pass_the_captured_amount_the_losers_get_409_and_an_audit_row_and_a_Failed_refund_frees_its_amount()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();

        // 5 x 40% of 25.00: at most two can fit (20.00), three must lose.
        IdentityTestEnv.Api[] results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => e.RefundAsync(order, "10.00")));

        Assert.Equal(2, results.Count(r => r.Status == HttpStatusCode.Accepted));
        Assert.Equal(3, results.Count(r => r.Status == HttpStatusCode.Conflict));
        Assert.True(await e.CommittedAsync(order) <= RefundEnv.Captured);
        Assert.Equal(20.00m, await e.CommittedAsync(order));
        Assert.Equal(3, await e.AuditCountAsync("refund.rejected"));
        Assert.Equal(2, await e.OpenRefundsCountAsync(order));

        // A Failed webhook frees that amount: a new 10.00 now fits, a further one does not.
        Guid winner = RefundEnv.RefundIdOf(results.First(r => r.Status == HttpStatusCode.Accepted));
        await e.RefundWebhookAsync(winner, "failed");
        Assert.Equal("Failed", await e.RefundStatusAsync(winner));
        Assert.Equal(HttpStatusCode.Accepted, (await e.RefundAsync(order, "10.00")).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await e.RefundAsync(order, "10.00")).Status);
        Assert.True(await e.CommittedAsync(order) <= RefundEnv.Captured);
    }

    // ---- Test 27: idempotency on three paths ----

    [Fact]
    public async Task T27_the_same_key_twice_returns_the_same_refund_and_one_row_one_audit_one_outbox_event()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();

        IdentityTestEnv.Api first = await e.RefundAsync(order, "5.00", key: "double-submit-1");
        IdentityTestEnv.Api second = await e.RefundAsync(order, "5.00", key: "double-submit-1");

        Assert.Equal(HttpStatusCode.Accepted, first.Status);
        Assert.Equal(HttpStatusCode.Accepted, second.Status);
        Assert.Equal(first.Text, second.Text);
        Assert.Equal(1, await e.OpenRefundsCountAsync(order));
        Assert.Equal(1, await e.AuditCountAsync("refund.request"));
        Assert.Equal(1, await e.F.OutboxCountAsync("RefundRequested"));
    }

    [Fact]
    public async Task T27_two_parallel_requests_with_one_key_make_one_refund()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();

        IdentityTestEnv.Api[] results = await Task.WhenAll(e.RefundAsync(order, "5.00", key: "race-1"), e.RefundAsync(order, "5.00", key: "race-1"));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.Accepted, r.Status));
        Assert.Equal(1, await e.OpenRefundsCountAsync(order));
        Assert.Equal(1, await e.AuditCountAsync("refund.request"));
    }

    [Fact]
    public async Task T27_the_same_key_with_a_different_body_is_409_and_creates_nothing()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        await e.RefundAsync(order, "5.00", key: "k-1");

        IdentityTestEnv.Api other = await e.RefundAsync(order, "6.00", key: "k-1");

        Assert.Equal(HttpStatusCode.Conflict, other.Status);
        Assert.Equal(1, await e.OpenRefundsCountAsync(order));
    }

    [Fact]
    public async Task T27_a_consumer_retry_makes_one_Stripe_refund_with_the_key_re_refundId_and_a_clean_run_calls_it_once()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        Guid refund = RefundEnv.RefundIdOf(await e.RefundAsync(order, "5.00"));

        // The provider is down for the first run: the event retries; then it works and the provider returns the same refund.
        e.F.Gateway.Unavailable = true;
        await e.Env.DrainOutboxAsync();
        e.F.Gateway.Unavailable = false;
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddSeconds(2);
        await e.Env.DrainOutboxAsync();
        await e.Env.DrainOutboxAsync(rounds: 2);

        Assert.All(e.F.Gateway.RefundCalls, c => Assert.Equal($"re:{refund}", c.IdempotencyKey));
        Assert.Equal(2, e.F.Gateway.RefundCalls.Count); // one failed attempt, one that worked; the third drain found nothing due
        Assert.Equal(1, e.F.Gateway.DistinctRefunds);
        Assert.Equal(e.F.Gateway.RefundIdOf($"re:{refund}"), await e.F.ScalarAsync<string>("SELECT provider_refund_id FROM refunds WHERE id = @r", ("r", refund)));
        Assert.Equal("Pending", await e.RefundStatusAsync(refund)); // only the webhook confirms
    }

    [Fact]
    public async Task T27_a_clean_consumer_run_calls_the_provider_exactly_once_and_a_second_run_does_not()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        Guid refund = RefundEnv.RefundIdOf(await e.RefundAsync(order, "5.00"));

        await e.Env.DrainOutboxAsync(rounds: 3);

        Assert.Equal(1, e.F.Gateway.RefundCalls.Count(c => c.IdempotencyKey == $"re:{refund}"));
        Assert.Empty(e.F.Gateway.Cancels);
        Assert.Equal(0, e.F.Gateway.TransactionViolations);
    }

    [Fact]
    public async Task T27_a_duplicate_refund_webhook_makes_one_transition_one_history_row_one_audit_row()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        Guid refund = RefundEnv.RefundIdOf(await e.RefundAsync(order, "25.00"));

        await e.RefundWebhookAsync(refund, "succeeded", eventId: "evt_dup");
        await e.RefundWebhookAsync(refund, "succeeded", eventId: "evt_dup"); // same event
        await e.RefundWebhookAsync(refund, "succeeded", eventId: "evt_other"); // a second terminal event for the same refund

        Assert.Equal("Succeeded", await e.RefundStatusAsync(refund));
        Assert.Equal("Refunded", await e.F.OrderStatusAsync(order));
        Assert.Equal(1, await e.HistoryCountAsync(order, "Refunded"));
        Assert.Equal(1, await e.AuditCountAsync("refund.succeeded"));
        Assert.Equal(1, await e.F.CountAsync("SELECT count(*) FROM outbox_events WHERE event_type = 'OrderStatusChanged' AND aggregate_id = @o", ("o", order)));
        Assert.Equal(1, await e.OpenRefundsCountAsync(order));
    }

    // ---- Test 28: states ----

    [Fact]
    public async Task T28_a_partial_refund_marks_the_payment_PartiallyRefunded_and_leaves_the_order_status_and_totals()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        string totals = await e.OrderTotalsAsync(order);
        IdentityTestEnv.Api accepted = await e.RefundAsync(order, "10.00");

        await e.RefundWebhookAsync(RefundEnv.RefundIdOf(accepted), "succeeded");

        Assert.Equal("PartiallyRefunded", await e.PaymentStatusAsync(order));
        Assert.Equal("Paid", await e.F.OrderStatusAsync(order));
        Assert.Equal(totals, await e.OrderTotalsAsync(order));
        Assert.Equal(0, await e.HistoryCountAsync(order, "Refunded"));
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Processing")]
    public async Task T28_refunds_reaching_the_captured_amount_make_the_payment_Refunded_and_the_order_Refunded(string status)
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync(status);
        string totals = await e.OrderTotalsAsync(order);
        Guid first = RefundEnv.RefundIdOf(await e.RefundAsync(order, "10.00"));
        Guid second = RefundEnv.RefundIdOf(await e.RefundAsync(order, "15.00"));

        await e.RefundWebhookAsync(first, "succeeded");
        Assert.Equal(status, await e.F.OrderStatusAsync(order));
        await e.RefundWebhookAsync(second, "succeeded");

        Assert.Equal("Refunded", await e.PaymentStatusAsync(order));
        Assert.Equal("Refunded", await e.F.OrderStatusAsync(order));
        Assert.Equal(1, await e.HistoryCountAsync(order, "Refunded"));
        Assert.Equal(totals, await e.OrderTotalsAsync(order));
    }

    [Fact]
    public async Task T28_a_full_refund_on_a_Shipped_order_is_409_but_a_partial_one_is_allowed()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync("Shipped");

        IdentityTestEnv.Api full = await e.RefundAsync(order, "25.00");
        IdentityTestEnv.Api partial = await e.RefundAsync(order, "5.00");

        Assert.Equal(HttpStatusCode.Conflict, full.Status);
        Assert.Equal(HttpStatusCode.Accepted, partial.Status);
        Assert.Equal(5.00m, await e.CommittedAsync(order));
    }

    [Theory]
    [InlineData("PendingPayment")]
    [InlineData("Cancelled")]
    [InlineData("Refunded")]
    [InlineData("Returned")]
    public async Task T28_no_refund_at_all_from_a_status_that_cannot_hold_one(string status)
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync(status);

        Assert.Equal(HttpStatusCode.Conflict, (await e.RefundAsync(order, "5.00")).Status);
        Assert.Equal(0, await e.OpenRefundsCountAsync(order));
    }

    [Fact]
    public async Task T28_ship_and_process_are_409_while_a_full_refund_is_Pending_and_work_again_once_it_Failed()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync("Processing");
        Guid refund = RefundEnv.RefundIdOf(await e.RefundAsync(order, "25.00"));

        IdentityTestEnv.Api ship = await e.OrderActionAsync(order, "ship", new { carrierCode = Orders.OrdersEnv.Carrier, trackingNumber = "TRK-1" });

        Assert.Equal(HttpStatusCode.Conflict, ship.Status);
        Assert.Equal("Processing", await e.F.OrderStatusAsync(order));
        Assert.Equal(0, await e.F.CountAsync("SELECT count(*) FROM shipments WHERE order_id = @o", ("o", order)));

        Guid paid = await e.SeedOrderAsync();
        Guid paidRefund = RefundEnv.RefundIdOf(await e.RefundAsync(paid, "25.00"));
        Assert.Equal(HttpStatusCode.Conflict, (await e.OrderActionAsync(paid, "process")).Status);

        await e.RefundWebhookAsync(refund, "failed");
        IdentityTestEnv.Api shipped = await e.OrderActionAsync(order, "ship", new { carrierCode = Orders.OrdersEnv.Carrier, trackingNumber = "TRK-1" });
        Assert.Equal(HttpStatusCode.NoContent, shipped.Status);
        Assert.Equal("Pending", await e.RefundStatusAsync(paidRefund));
    }

    [Fact]
    public async Task T28_a_Delivered_order_with_an_approved_return_goes_to_Returned_and_the_return_becomes_Refunded()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync("Delivered");
        Guid variant = await e.VariantOfAsync(order);

        // Without an approved return a full refund on a Delivered order is 409.
        Assert.Equal(HttpStatusCode.Conflict, (await e.RefundAsync(order, "25.00")).Status);

        IdentityTestEnv.Api request = await e.Env.PostAsync(
            $"/api/v1/orders/{order}/return-request", new { items = new[] { new { variantId = variant, quantity = 1 } }, reason = "Wrong size" }, e.TokenA);
        Assert.Equal(HttpStatusCode.Created, request.Status);
        Guid returnId = request.Json.GetProperty("returnId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await e.Env.PostAsync($"/api/v1/admin/returns/{returnId}/approve", new { reason = "ok" }, e.Admin)).Status);

        Guid refund = RefundEnv.RefundIdOf(await e.RefundAsync(order, "25.00"));
        await e.RefundWebhookAsync(refund, "succeeded");

        Assert.Equal("Returned", await e.F.OrderStatusAsync(order));
        Assert.Equal("Refunded", await e.PaymentStatusAsync(order));
        Assert.Equal("Refunded", await e.F.ScalarAsync<string>("SELECT status FROM return_requests WHERE id = @r", ("r", returnId)));
        Assert.Equal(1, await e.HistoryCountAsync(order, "Returned"));
    }

    [Fact]
    public async Task T28_a_partial_refund_on_a_Delivered_order_marks_its_approved_return_Refunded_without_moving_the_order()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync("Delivered");
        Guid variant = await e.VariantOfAsync(order);
        Guid returnId = (await e.Env.PostAsync(
            $"/api/v1/orders/{order}/return-request", new { items = new[] { new { variantId = variant, quantity = 1 } }, reason = "Wrong size" }, e.TokenA))
            .Json.GetProperty("returnId").GetGuid();
        await e.Env.PostAsync($"/api/v1/admin/returns/{returnId}/approve", new { reason = "ok" }, e.Admin);

        await e.RefundWebhookAsync(RefundEnv.RefundIdOf(await e.RefundAsync(order, "10.00")), "succeeded");

        Assert.Equal("Delivered", await e.F.OrderStatusAsync(order));
        Assert.Equal("Refunded", await e.F.ScalarAsync<string>("SELECT status FROM return_requests WHERE id = @r", ("r", returnId)));
    }

    [Fact]
    public async Task T28_the_audit_row_has_actor_before_after_and_reason_and_the_202_carries_the_servers_totals()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        Guid adminId = await e.F.ScalarAsync<Guid>("SELECT id FROM users WHERE email = 'admin@example.test'");

        IdentityTestEnv.Api accepted = await e.RefundAsync(order, "10.00", reason: "Item arrived scratched");

        Assert.Equal(HttpStatusCode.Accepted, accepted.Status);
        Assert.Equal("Pending", accepted.Json.GetProperty("status").GetString());
        Assert.Equal("10.00", accepted.Json.GetProperty("amount").GetProperty("amount").GetString());
        Assert.Equal("0.00", accepted.Json.GetProperty("refundedTotal").GetProperty("amount").GetString());
        Assert.Equal("15.00", accepted.Json.GetProperty("remaining").GetProperty("amount").GetString());
        Guid refund = RefundEnv.RefundIdOf(accepted);
        Assert.Equal(adminId, await e.F.ScalarAsync<Guid>("SELECT actor_id FROM audit_log WHERE action = 'refund.request' AND entity_id = @r", ("r", refund)));
        Assert.Equal("Item arrived scratched", await e.F.ScalarAsync<string>("SELECT reason FROM audit_log WHERE action = 'refund.request' AND entity_id = @r", ("r", refund)));
        string before = await e.F.ScalarAsync<string>("SELECT before_json::text FROM audit_log WHERE action = 'refund.request' AND entity_id = @r", ("r", refund));
        string after = await e.F.ScalarAsync<string>("SELECT after_json::text FROM audit_log WHERE action = 'refund.request' AND entity_id = @r", ("r", refund));
        Assert.Contains("\"pendingTotal\": 0", before);
        Assert.Contains("\"pendingTotal\": 10", after);
    }

    [Fact]
    public async Task Two_refunds_succeeding_in_one_event_move_the_order_once_with_no_alert_and_true_audit_rows()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        Guid a = RefundEnv.RefundIdOf(await e.RefundAsync(order, "12.50"));
        Guid b = RefundEnv.RefundIdOf(await e.RefundAsync(order, "12.50"));
        string body = "{\"id\":\"evt_two\",\"type\":\"charge.refunded\",\"data\":{\"object\":{\"id\":\"ch_1\",\"refunds\":{\"data\":["
            + $"{{\"id\":\"re_a\",\"status\":\"succeeded\",\"metadata\":{{\"refund_id\":\"{a}\"}}}},"
            + $"{{\"id\":\"re_b\",\"status\":\"succeeded\",\"metadata\":{{\"refund_id\":\"{b}\"}}}}]}}}}}}}}";

        Assert.Equal(HttpStatusCode.OK, (await e.F.WebhookRawAsync(body, e.F.Sign(body, e.Env.Clock.UtcNow))).Status);

        Assert.Equal("Refunded", await e.F.OrderStatusAsync(order));
        Assert.Equal(1, await e.HistoryCountAsync(order, "Refunded"));
        Assert.DoesNotContain(e.Env.Logs.Events, l => l.RenderMessage().Contains("could not move"));
        Assert.Equal(2, await e.F.CountAsync("SELECT count(*) FROM audit_log WHERE action = 'refund.succeeded' AND after_json->>'OrderTransitioned' = 'true'"));
    }


    // ---- Test 7 (refund part) and endpoint validation ----

    [Fact]
    public async Task T7_refund_as_Customer_or_Support_is_403_and_anonymous_is_401_and_nothing_is_created()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await e.RefundAsync(order, "5.00", token: e.TokenA)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.RefundAsync(order, "5.00", token: e.TokenB)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.RefundAsync(order, "5.00", token: e.Support)).Status);
        IdentityTestEnv.Api anonymous = await e.Env.PostAsync(
            $"/api/v1/admin/orders/{order}/refund", new { amount = new { amount = "5.00", currency = "USD" }, reason = "x" }, null, ("Idempotency-Key", "anon-1"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.Equal(0, await e.OpenRefundsCountAsync(order));
    }

    [Theory]
    [InlineData("0.00", "USD", "Damaged", true)]
    [InlineData("-5.00", "USD", "Damaged", true)]
    [InlineData("abc", "USD", "Damaged", true)]
    [InlineData("1e1", "USD", "Damaged", true)]
    [InlineData("5.001", "USD", "Damaged", true)]
    [InlineData("5.00", "EUR", "Damaged", true)]
    [InlineData("5.00", "USD", "", true)]
    [InlineData("5.00", "USD", "Damaged", false)]
    public async Task A_bad_refund_request_is_400_and_creates_nothing(string amount, string currency, string reason, bool withKey)
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();

        IdentityTestEnv.Api r = await e.RefundAsync(order, amount, key: withKey ? "" : null, reason: reason, currency: currency);

        Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        Assert.Equal(0, await e.OpenRefundsCountAsync(order));
    }

    [Fact]
    public async Task A_refund_for_an_unknown_order_is_404_and_for_an_order_with_no_captured_payment_is_409()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid unpaid = await Orders.OrderSeed.InsertAsync(e.Cs, e.CustomerId, "PendingPayment", e.Env.Clock.UtcNow);

        Assert.Equal(HttpStatusCode.NotFound, (await e.RefundAsync(Guid.NewGuid(), "5.00")).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await e.RefundAsync(unpaid, "5.00")).Status);
    }

    [Fact]
    public async Task A_refund_body_with_an_unknown_member_is_400()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();

        IdentityTestEnv.Api r = await e.Env.PostAsync(
            $"/api/v1/admin/orders/{order}/refund", new { amount = new { amount = "5.00", currency = "USD" }, reason = "x", orderStatus = "Refunded" }, e.Admin, ("Idempotency-Key", "k-extra"));

        Assert.Equal(HttpStatusCode.BadRequest, r.Status);
    }

    [Fact]
    public async Task An_unknown_refund_webhook_is_200_and_a_bad_signature_is_400()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);

        Assert.Equal(HttpStatusCode.OK, (await e.RefundWebhookRawAsync(Guid.NewGuid(), "succeeded")).Status);
        string body = "{\"id\":\"evt_x\",\"type\":\"refund.updated\",\"data\":{\"object\":{\"id\":\"re_1\",\"status\":\"succeeded\"}}}";
        Assert.Equal(HttpStatusCode.BadRequest, (await e.F.WebhookRawAsync(body, "t=1,v1=bad")).Status);
    }
}
