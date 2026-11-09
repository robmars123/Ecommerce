using System.Net;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Inventory;

namespace Ecommerce.Api.IntegrationTests.Checkout;

/// <summary>Retry payment (arch 8.2 rule 11: tests 24, 7 part) and reservation expiry (rule 7: test 4).</summary>
public class RetryAndExpiryTests(PostgresFixture postgres)
{
    private static string Key() => "key-" + Guid.NewGuid().ToString("N");

    private async Task<(CheckoutEnv Env, Guid Variant, Guid OrderId)> PlacedAsync(string sku)
    {
        CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync(sku, stock: 10);
        Guid orderId = await f.PlaceOrderAsync(variant, 2, Key());
        return (f, variant, orderId);
    }

    [Fact]
    public async Task T24_a_retry_adds_attempt_2_with_a_new_intent_the_same_amount_and_the_same_reservation()
    {
        (CheckoutEnv f, Guid variant, Guid orderId) = await PlacedAsync("RETRY-1");
        await using CheckoutEnv owner = f;
        DateTimeOffset expiresBefore = await f.ScalarAsync<DateTimeOffset>("SELECT expires_at FROM inventory_reservations WHERE order_id = @o", ("o", orderId));
        Guid reservationBefore = await f.ScalarAsync<Guid>("SELECT id FROM inventory_reservations WHERE order_id = @o", ("o", orderId));
        f.Env.Clock.UtcNow = f.Env.Clock.UtcNow.AddMinutes(5);
        string token = await f.Env.AccessTokenAsync("a@example.test");

        IdentityTestEnv.Api r = await f.RetryAsync(token, orderId, Key());

        Assert.Equal(HttpStatusCode.OK, r.Status);
        Assert.Equal(orderId, r.Json.GetProperty("orderId").GetGuid());
        Assert.Equal(2, r.Json.GetProperty("attempt").GetInt32());
        Assert.Equal(2, await f.CountAsync("SELECT count(*) FROM payments WHERE order_id = @o", ("o", orderId)));
        Assert.NotEqual(await f.IntentOfAsync(orderId, 1), await f.IntentOfAsync(orderId, 2));
        Assert.Equal(2, f.Gateway.Intents.Count);
        Assert.Equal([$"pi:{orderId}:1", $"pi:{orderId}:2"], f.Gateway.Creates.Select(c => c.IdempotencyKey));
        Assert.Equal(2500, f.Gateway.Intents.Select(i => i.Request.AmountMinor).Distinct().Single());
        Assert.Equal(2, await f.CountAsync("SELECT count(*) FROM payments WHERE order_id = @o AND amount = 25.00 AND status = 'Pending'", ("o", orderId)));
        Assert.Equal(f.Gateway.Intents[1].Secret, r.Str("clientSecret"));
        // The reservation is untouched: same row, same expires_at (a retry never extends the TTL).
        Assert.Equal(reservationBefore, await f.ScalarAsync<Guid>("SELECT id FROM inventory_reservations WHERE order_id = @o", ("o", orderId)));
        Assert.Equal(expiresBefore, await f.ScalarAsync<DateTimeOffset>("SELECT expires_at FROM inventory_reservations WHERE order_id = @o", ("o", orderId)));
        Assert.Equal((10, 2), await InventoryEnv.ReadAsync(f.Env, variant));
        // The superseded intent is cancelled through the outbox.
        Assert.Equal(1, await f.OutboxCountAsync("PaymentIntentCancelRequested"));
    }

    [Fact]
    public async Task T24_the_same_key_twice_makes_one_attempt_and_returns_the_same_secret()
    {
        (CheckoutEnv f, _, Guid orderId) = await PlacedAsync("RETRY-2");
        await using CheckoutEnv owner = f;
        string key = Key();

        IdentityTestEnv.Api first = await f.RetryAsync(f.TokenA, orderId, key);
        IdentityTestEnv.Api second = await f.RetryAsync(f.TokenA, orderId, key);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(first.Text, second.Text);
        Assert.Equal(2, first.Json.GetProperty("attempt").GetInt32());
        Assert.Equal(2, await f.CountAsync("SELECT count(*) FROM payments WHERE order_id = @o", ("o", orderId)));
        Assert.Equal(1, await f.OutboxCountAsync("PaymentIntentCancelRequested"));
    }

    [Fact]
    public async Task T24_the_same_key_for_another_order_or_user_is_a_409()
    {
        (CheckoutEnv f, _, Guid orderId) = await PlacedAsync("RETRY-3");
        await using CheckoutEnv owner = f;
        Guid secondVariant = await f.Cart.VariantAsync("RETRY-3B", stock: 10);
        Guid secondOrder = await f.PlaceOrderAsync(secondVariant, 1, Key());
        string key = Key();
        Assert.Equal(HttpStatusCode.OK, (await f.RetryAsync(f.TokenA, orderId, key)).Status);

        IdentityTestEnv.Api other = await f.RetryAsync(f.TokenA, secondOrder, key);

        Assert.Equal(HttpStatusCode.Conflict, other.Status);
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM payments WHERE order_id = @o", ("o", secondOrder)));
    }

    [Fact]
    public async Task T24_a_retry_is_only_allowed_while_the_order_is_PendingPayment()
    {
        (CheckoutEnv f, _, Guid orderId) = await PlacedAsync("RETRY-4");
        await using CheckoutEnv owner = f;
        await f.PayAsync(orderId, "evt_r4");

        IdentityTestEnv.Api r = await f.RetryAsync(f.TokenA, orderId, Key());

        Assert.Equal(HttpStatusCode.Conflict, r.Status);
        Assert.Equal("not-pending-payment", r.Str("type"));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM payments WHERE order_id = @o", ("o", orderId)));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM idempotency_keys WHERE scope = 'retry-payment'"));
    }

    [Fact]
    public async Task T24_past_the_reservation_expiry_a_retry_is_a_409_even_before_the_sweep_runs_and_creates_no_payment()
    {
        (CheckoutEnv f, _, Guid orderId) = await PlacedAsync("RETRY-5");
        await using CheckoutEnv owner = f;
        f.Env.Clock.UtcNow = f.Env.Clock.UtcNow.AddMinutes(16);
        string token = await f.Env.AccessTokenAsync("a@example.test");

        IdentityTestEnv.Api r = await f.RetryAsync(token, orderId, Key());

        Assert.Equal(HttpStatusCode.Conflict, r.Status);
        Assert.Equal("reservation-expired", r.Str("type"));
        Assert.Equal("PendingPayment", await f.OrderStatusAsync(orderId));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM payments WHERE order_id = @o", ("o", orderId)));
        Assert.Equal(0, await f.OutboxCountAsync("PaymentIntentCancelRequested"));
    }

    [Fact]
    public async Task T7_part_another_customer_retrying_gets_403_and_no_payment_is_written_and_an_unknown_order_is_404()
    {
        (CheckoutEnv f, _, Guid orderId) = await PlacedAsync("RETRY-7");
        await using CheckoutEnv owner = f;

        IdentityTestEnv.Api foreign = await f.RetryAsync(f.TokenB, orderId, Key());
        IdentityTestEnv.Api unknown = await f.RetryAsync(f.TokenA, Guid.NewGuid(), Key());
        IdentityTestEnv.Api noKey = await f.RetryAsync(f.TokenA, orderId, null);

        Assert.Equal(HttpStatusCode.Forbidden, foreign.Status);
        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Equal(HttpStatusCode.BadRequest, noKey.Status);
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM payments WHERE order_id = @o", ("o", orderId)));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM idempotency_keys WHERE scope = 'retry-payment'"));
    }

    [Fact]
    public async Task A_provider_outage_during_retry_is_a_503_with_the_order_id_and_the_same_key_heals_it()
    {
        (CheckoutEnv f, _, Guid orderId) = await PlacedAsync("RETRY-8");
        await using CheckoutEnv owner = f;
        string key = Key();
        f.Gateway.Unavailable = true;

        IdentityTestEnv.Api down = await f.RetryAsync(f.TokenA, orderId, key);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, down.Status);
        Assert.Equal(orderId, Guid.Parse(down.Str("orderId")));
        f.Gateway.Unavailable = false;

        IdentityTestEnv.Api healed = await f.RetryAsync(f.TokenA, orderId, key);

        Assert.Equal(HttpStatusCode.OK, healed.Status);
        Assert.Equal(2, healed.Json.GetProperty("attempt").GetInt32());
        Assert.Equal(2, await f.CountAsync("SELECT count(*) FROM payments WHERE order_id = @o", ("o", orderId)));
    }

    // ---- expiry ----

    [Fact]
    public async Task T4_after_the_TTL_the_sweep_expires_the_reservation_releases_stock_cancels_the_order_and_a_new_checkout_succeeds()
    {
        (CheckoutEnv f, Guid variant, Guid orderId) = await PlacedAsync("EXP-1");
        await using CheckoutEnv owner = f;
        f.Env.Clock.UtcNow = f.Env.Clock.UtcNow.AddMinutes(16);

        Assert.Equal(1, await f.SweepAsync());
        Assert.Equal(0, await f.SweepAsync());

        Assert.Equal("Cancelled", await f.OrderStatusAsync(orderId));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM inventory_reservations WHERE order_id = @o AND status = 'Expired'", ("o", orderId)));
        Assert.Equal((10, 0), await InventoryEnv.ReadAsync(f.Env, variant));
        Assert.Equal(1, await f.CountAsync(
            "SELECT count(*) FROM order_status_history WHERE order_id = @o AND from_status = 'PendingPayment' AND to_status = 'Cancelled' AND changed_by IS NULL AND reason = 'reservation-expired'",
            ("o", orderId)));
        Assert.Equal(1, await f.OutboxCountAsync("PaymentIntentCancelRequested"));

        // The cart is kept on expiry, so the customer can simply check out again.
        string token = await f.Env.AccessTokenAsync("a@example.test");
        IdentityTestEnv.Api again = await f.CheckoutAsync(token, Key());

        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.NotEqual(orderId, again.Json.GetProperty("order").GetProperty("id").GetGuid());
        Assert.Equal((10, 2), await InventoryEnv.ReadAsync(f.Env, variant));
    }

    [Fact]
    public async Task T4_an_order_paid_before_the_sweep_is_not_cancelled_and_its_stock_is_not_released()
    {
        (CheckoutEnv f, Guid variant, Guid orderId) = await PlacedAsync("EXP-2");
        await using CheckoutEnv owner = f;
        await f.PayAsync(orderId, "evt_e2");
        f.Env.Clock.UtcNow = f.Env.Clock.UtcNow.AddMinutes(16);

        Assert.Equal(0, await f.SweepAsync());

        Assert.Equal("Paid", await f.OrderStatusAsync(orderId));
        Assert.Equal((8, 0), await InventoryEnv.ReadAsync(f.Env, variant));
        Assert.Equal(0, await f.OutboxCountAsync("PaymentIntentCancelRequested"));
    }

    [Fact]
    public async Task T4_the_last_unit_held_by_an_unpaid_order_comes_back_after_the_TTL_without_manual_help()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("EXP-3", stock: 1);
        await f.PlaceOrderAsync(variant, 1, Key());
        IdentityTestEnv.Api blocked = await f.Cart.AddAsync(f.TokenB, variant, 1);
        f.Env.Clock.UtcNow = f.Env.Clock.UtcNow.AddMinutes(16);

        Assert.Equal(1, await f.SweepAsync());
        string tokenB = await f.Env.AccessTokenAsync("b@example.test");
        IdentityTestEnv.Api added = await f.Cart.AddAsync(tokenB, variant, 1);
        IdentityTestEnv.Api afterExpiry = await f.CheckoutAsync(tokenB, Key());

        Assert.Equal(HttpStatusCode.Conflict, blocked.Status);
        Assert.Equal(HttpStatusCode.OK, added.Status);
        Assert.Equal(HttpStatusCode.OK, afterExpiry.Status);
    }
}
