using System.Net;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Inventory;
using Ecommerce.Orders.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Events;

namespace Ecommerce.Api.IntegrationTests.Checkout;

/// <summary>The signed webhook (arch 8.2 rules 6, 8, 12): tests 3, 8, 19, 25, signature checks and test 29 (payment status).</summary>
public class WebhookTests(PostgresFixture postgres)
{
    private static string Key() => "key-" + Guid.NewGuid().ToString("N");

    /// <summary>Runs the real handler, then throws once: the whole webhook transaction must roll back (Test 8).</summary>
    private sealed class ThrowOnceOrderPayments(IOrderPayments inner, ThrowOnceState state) : IOrderPayments
    {
        public async Task<MarkPaidOutcome> TryMarkPaidAsync(Guid orderId, CancellationToken ct)
        {
            MarkPaidOutcome outcome = await inner.TryMarkPaidAsync(orderId, ct);
            if (state.TryTrip())
            {
                throw new InvalidOperationException("injected failure after the order was marked Paid");
            }
            return outcome;
        }
    }

    private sealed class ThrowOnceState
    {
        private int _armed = 1;

        public bool TryTrip() => Interlocked.Exchange(ref _armed, 0) == 1;
    }

    private async Task<(CheckoutEnv Env, Guid Variant, Guid OrderId)> PlacedAsync(string sku, int quantity = 2, CheckoutEnv? existing = null)
    {
        CheckoutEnv f = existing ?? await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync(sku, stock: 10);
        Guid orderId = await f.PlaceOrderAsync(variant, quantity, Key());
        return (f, variant, orderId);
    }

    [Fact]
    public async Task T3_the_same_event_twice_in_a_row_marks_the_order_Paid_once_and_both_answer_200()
    {
        (CheckoutEnv f, Guid variant, Guid orderId) = await PlacedAsync("HOOK-1");
        await using CheckoutEnv owner = f;
        string intent = await f.IntentOfAsync(orderId);

        IdentityTestEnv.Api first = await f.WebhookAsync("evt_3a", intent);
        IdentityTestEnv.Api second = await f.WebhookAsync("evt_3a", intent);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        await AssertPaidOnceAsync(f, orderId, variant, quantity: 2);
    }

    [Fact]
    public async Task T3_the_same_event_delivered_concurrently_marks_the_order_Paid_once_and_all_answer_200()
    {
        (CheckoutEnv f, Guid variant, Guid orderId) = await PlacedAsync("HOOK-2");
        await using CheckoutEnv owner = f;
        string intent = await f.IntentOfAsync(orderId);

        IdentityTestEnv.Api[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => f.WebhookAsync("evt_3b", intent)));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.Status));
        await AssertPaidOnceAsync(f, orderId, variant, quantity: 2);
    }

    private static async Task AssertPaidOnceAsync(CheckoutEnv f, Guid orderId, Guid variant, int quantity)
    {
        Assert.Equal("Paid", await f.OrderStatusAsync(orderId));
        Assert.Equal(1, await f.CountAsync(
            "SELECT count(*) FROM order_status_history WHERE order_id = @o AND from_status = 'PendingPayment' AND to_status = 'Paid'", ("o", orderId)));
        Assert.Equal(1, await f.OutboxCountAsync("OrderPaid"));
        Assert.Equal((10 - quantity, 0), await InventoryEnv.ReadAsync(f.Env, variant));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM inventory_reservations WHERE status = 'Committed'"));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM idempotency_keys WHERE scope = 'stripe-event'"));
        Assert.Equal("Succeeded", await f.ScalarAsync<string>("SELECT status FROM payments WHERE order_id = @o", ("o", orderId)));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM refunds"));
    }

    [Fact]
    public async Task Paying_removes_the_ordered_lines_from_the_cart()
    {
        (CheckoutEnv f, Guid variant, Guid orderId) = await PlacedAsync("HOOK-CART");
        await using CheckoutEnv owner = f;
        Guid other = await f.Cart.VariantAsync("HOOK-CART-2");
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, other, 1)).Status);
        Assert.Contains(variant, (await f.Cart.QuantitiesAsync(f.TokenA)).Keys);

        await f.PayAsync(orderId, "evt_cart");

        Dictionary<Guid, int> cart = await f.Cart.QuantitiesAsync(f.TokenA);
        Assert.DoesNotContain(variant, cart.Keys);
        Assert.Equal(1, cart[other]);
    }

    [Fact]
    public async Task T8_a_failure_inside_the_webhook_transaction_is_a_500_that_commits_nothing_and_the_redelivery_pays_once()
    {
        ThrowOnceState trip = new();
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres, configure: services =>
        {
            services.RemoveAll<IOrderPayments>();
            services.AddScoped<IOrderPayments>(sp =>
                new ThrowOnceOrderPayments(sp.GetRequiredService<Ecommerce.Orders.Application.OrderPaymentHandler>(), trip));
        });
        Guid variant = await f.Cart.VariantAsync("HOOK-8", stock: 10);
        Guid orderId = await f.PlaceOrderAsync(variant, 2, Key());
        string intent = await f.IntentOfAsync(orderId);

        IdentityTestEnv.Api failed = await f.WebhookAsync("evt_8", intent);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.Status);
        Assert.Equal("PendingPayment", await f.OrderStatusAsync(orderId));
        Assert.Equal("Pending", await f.ScalarAsync<string>("SELECT status FROM payments WHERE order_id = @o", ("o", orderId)));
        Assert.Equal((10, 2), await InventoryEnv.ReadAsync(f.Env, variant));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM outbox_events WHERE event_type = 'OrderPaid'"));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM idempotency_keys WHERE scope = 'stripe-event'"));

        IdentityTestEnv.Api redelivered = await f.WebhookAsync("evt_8", intent);

        Assert.Equal(HttpStatusCode.OK, redelivered.Status);
        await AssertPaidOnceAsync(f, orderId, variant, quantity: 2);
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM orders"));
    }

    [Fact]
    public async Task A_missing_bad_or_stale_signature_is_a_400_and_changes_nothing()
    {
        (CheckoutEnv f, Guid variant, Guid orderId) = await PlacedAsync("HOOK-SIG");
        await using CheckoutEnv owner = f;
        string body = CheckoutEnv.EventBody("evt_sig", "payment_intent.succeeded", await f.IntentOfAsync(orderId));
        DateTimeOffset now = f.Env.Clock.UtcNow;

        IdentityTestEnv.Api missing = await f.WebhookRawAsync(body, null);
        IdentityTestEnv.Api wrongSecret = await f.WebhookRawAsync(body, f.Sign(body, now, secret: "not-the-secret"));
        IdentityTestEnv.Api tampered = await f.WebhookRawAsync(body + " ", f.Sign(body, now));
        IdentityTestEnv.Api stale = await f.WebhookRawAsync(body, f.Sign(body, now.AddHours(-1)));
        IdentityTestEnv.Api garbage = await f.WebhookRawAsync(body, "t=1,v1=zz");

        foreach (IdentityTestEnv.Api r in new[] { missing, wrongSecret, tampered, stale, garbage })
        {
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        }
        Assert.Equal("PendingPayment", await f.OrderStatusAsync(orderId));
        Assert.Equal((10, 2), await InventoryEnv.ReadAsync(f.Env, variant));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM idempotency_keys WHERE scope = 'stripe-event'"));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM outbox_events WHERE event_type = 'OrderPaid'"));
    }

    [Fact]
    public async Task A_valid_signature_over_the_exact_body_is_accepted()
    {
        (CheckoutEnv f, _, Guid orderId) = await PlacedAsync("HOOK-OK");
        await using CheckoutEnv owner = f;
        string body = CheckoutEnv.EventBody("evt_ok", "payment_intent.succeeded", await f.IntentOfAsync(orderId));

        IdentityTestEnv.Api r = await f.WebhookRawAsync(body, f.Sign(body, f.Env.Clock.UtcNow.AddMinutes(-4)));

        Assert.Equal(HttpStatusCode.OK, r.Status);
        Assert.Equal("Paid", await f.OrderStatusAsync(orderId));
    }

    [Fact]
    public async Task An_unknown_intent_and_an_event_type_we_ignore_are_200_and_change_nothing()
    {
        (CheckoutEnv f, _, Guid orderId) = await PlacedAsync("HOOK-UNK");
        await using CheckoutEnv owner = f;

        IdentityTestEnv.Api unknown = await f.WebhookAsync("evt_unk", "pi_does_not_exist");
        IdentityTestEnv.Api ignored = await f.WebhookAsync("evt_ign", await f.IntentOfAsync(orderId), "charge.refunded");

        Assert.Equal(HttpStatusCode.OK, unknown.Status);
        Assert.Equal(HttpStatusCode.OK, ignored.Status);
        Assert.Equal("PendingPayment", await f.OrderStatusAsync(orderId));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM idempotency_keys WHERE scope = 'stripe-event'"));
        Assert.Contains(f.Env.Logs.Events, e => e.Level == LogEventLevel.Warning && e.MessageTemplate.Text.Contains("unknown payment intent"));
    }

    [Fact]
    public async Task A_failed_or_canceled_payment_marks_only_the_payment_Failed_and_the_order_can_still_be_paid_by_the_same_intent()
    {
        (CheckoutEnv f, _, Guid orderId) = await PlacedAsync("HOOK-FAIL");
        await using CheckoutEnv owner = f;
        string intent = await f.IntentOfAsync(orderId);

        Assert.Equal(HttpStatusCode.OK, (await f.WebhookAsync("evt_f1", intent, "payment_intent.payment_failed")).Status);

        Assert.Equal("PendingPayment", await f.OrderStatusAsync(orderId));
        Assert.Equal("Failed", await f.ScalarAsync<string>("SELECT status FROM payments WHERE order_id = @o", ("o", orderId)));

        // The customer retried the declined card in the payment sheet and it worked: Failed to Succeeded (D8).
        Assert.Equal(HttpStatusCode.OK, (await f.WebhookAsync("evt_f2", intent)).Status);

        Assert.Equal("Paid", await f.OrderStatusAsync(orderId));
        Assert.Equal("Succeeded", await f.ScalarAsync<string>("SELECT status FROM payments WHERE order_id = @o", ("o", orderId)));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM refunds"));
    }

    [Fact]
    public async Task T19_a_success_for_a_Cancelled_order_never_resurrects_it_and_refunds_once_with_an_audit_row_and_an_alert()
    {
        (CheckoutEnv f, Guid variant, Guid orderId) = await PlacedAsync("HOOK-19");
        await using CheckoutEnv owner = f;
        string intent = await f.IntentOfAsync(orderId);
        f.Env.Clock.UtcNow = f.Env.Clock.UtcNow.AddMinutes(16);
        Assert.Equal(1, await f.SweepAsync());
        Assert.Equal("Cancelled", await f.OrderStatusAsync(orderId));

        IdentityTestEnv.Api first = await f.WebhookAsync("evt_19a", intent);
        IdentityTestEnv.Api duplicate = await f.WebhookAsync("evt_19a", intent);
        IdentityTestEnv.Api secondEvent = await f.WebhookAsync("evt_19b", intent);

        Assert.All(new[] { first, duplicate, secondEvent }, r => Assert.Equal(HttpStatusCode.OK, r.Status));
        Guid paymentId = await f.ScalarAsync<Guid>("SELECT id FROM payments WHERE order_id = @o", ("o", orderId));
        Assert.Equal("Cancelled", await f.OrderStatusAsync(orderId));
        Assert.Equal("Succeeded", await f.ScalarAsync<string>("SELECT status FROM payments WHERE id = @p", ("p", paymentId)));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM refunds"));
        Assert.Equal(1, await f.CountAsync(
            "SELECT count(*) FROM refunds WHERE payment_id = @p AND idempotency_key = @k AND actor_id IS NULL AND status = 'Pending' AND amount = 25.00 AND reason = 'late-success'",
            ("p", paymentId), ("k", $"auto:{paymentId}")));
        Assert.Equal(1, await f.OutboxCountAsync("RefundRequested"));
        Assert.Equal(0, await f.OutboxCountAsync("OrderPaid"));
        Assert.Equal(1, await f.CountAsync(
            "SELECT count(*) FROM audit_log WHERE action = 'payment.auto-refund' AND entity_id = @p AND actor_id IS NULL", ("p", paymentId)));
        Assert.Equal(2, await f.CountAsync("SELECT count(*) FROM order_status_history WHERE order_id = @o", ("o", orderId)));
        Assert.Equal((10, 0), await InventoryEnv.ReadAsync(f.Env, variant));
        LogEvent alert = Assert.Single(f.Env.Logs.Events, e => e.Properties.TryGetValue("EventId", out LogEventPropertyValue? id)
                                                            && id is StructureValue s && s.Properties.Any(p => p.Name == "Name" && p.Value.ToString() == "\"PaymentAlert\""));
        Assert.Equal(LogEventLevel.Fatal, alert.Level);
    }

    [Fact]
    public async Task T25_a_second_attempt_that_succeeds_after_the_order_is_Paid_is_recorded_and_refunded_once()
    {
        (CheckoutEnv f, Guid variant, Guid orderId) = await PlacedAsync("HOOK-25");
        await using CheckoutEnv owner = f;
        Assert.Equal(HttpStatusCode.OK, (await f.RetryAsync(f.TokenA, orderId, Key())).Status);
        string attempt1 = await f.IntentOfAsync(orderId, 1);
        string attempt2 = await f.IntentOfAsync(orderId, 2);
        Assert.NotEqual(attempt1, attempt2);

        Assert.Equal(HttpStatusCode.OK, (await f.WebhookAsync("evt_25a", attempt1)).Status);
        Assert.Equal(HttpStatusCode.OK, (await f.WebhookAsync("evt_25b", attempt2)).Status);
        Assert.Equal(HttpStatusCode.OK, (await f.WebhookAsync("evt_25b", attempt2)).Status);

        Guid payment2 = await f.PaymentIdAsync(orderId, 2);
        Assert.Equal("Paid", await f.OrderStatusAsync(orderId));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM order_status_history WHERE order_id = @o AND to_status = 'Paid'", ("o", orderId)));
        Assert.Equal((8, 0), await InventoryEnv.ReadAsync(f.Env, variant));
        Assert.Equal(1, await f.OutboxCountAsync("OrderPaid"));
        Assert.Equal("Succeeded", await f.ScalarAsync<string>("SELECT status FROM payments WHERE id = @p", ("p", payment2)));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM refunds"));
        Assert.Equal(1, await f.CountAsync(
            "SELECT count(*) FROM refunds WHERE payment_id = @p AND idempotency_key = @k AND reason = 'extra-success'", ("p", payment2), ("k", $"auto:{payment2}")));
        Assert.Equal(1, await f.OutboxCountAsync("RefundRequested"));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM audit_log WHERE action = 'payment.auto-refund'"));
        Assert.Contains(f.Env.Logs.Events, e => e.Level == LogEventLevel.Fatal);
    }

    [Fact]
    public async Task T29_the_order_detail_shows_the_latest_payment_status_before_and_after_Paid_and_null_when_there_is_none()
    {
        (CheckoutEnv f, _, Guid orderId) = await PlacedAsync("HOOK-29");
        await using CheckoutEnv owner = f;

        IdentityTestEnv.Api before = await f.OrderAsync(f.TokenA, orderId);
        await f.PayAsync(orderId, "evt_29");
        IdentityTestEnv.Api after = await f.OrderAsync(f.TokenA, orderId);

        Assert.Equal("Pending", before.Str("paymentStatus"));
        Assert.Equal("Succeeded", after.Str("paymentStatus"));
        Assert.Equal("Paid", after.Str("status"));

        Guid user = await f.ScalarAsync<Guid>("SELECT user_id FROM orders WHERE id = @o", ("o", orderId));
        Guid seeded = await Orders.OrderSeed.InsertAsync(InventoryEnv.Cs(f.Env), user, "Paid", f.Env.Clock.UtcNow);
        IdentityTestEnv.Api none = await f.OrderAsync(f.TokenA, seeded);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, none.Json.GetProperty("paymentStatus").ValueKind);
    }

}
