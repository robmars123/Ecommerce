using System.Net;
using Ecommerce.Api.IntegrationTests.Checkout;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Inventory;
using Ecommerce.Api.IntegrationTests.Orders;
using Ecommerce.Identity.Domain;

namespace Ecommerce.Api.IntegrationTests.Refunds;

/// <summary>
/// Slice 6 fixture: customers A and B, an Admin and a Support user, and orders seeded in any status with a captured payment
/// (25.00 USD: one 20.00 line plus 5.00 shipping). The provider is the fake gateway; nothing here reaches Stripe.
/// </summary>
internal sealed class RefundEnv : IAsyncDisposable
{
    public const decimal Captured = 25.00m;

    private RefundEnv(CheckoutEnv f, Guid customerId, string admin, string support)
    {
        F = f;
        CustomerId = customerId;
        Admin = admin;
        Support = support;
    }

    public CheckoutEnv F { get; }
    public IdentityTestEnv Env => F.Env;
    public Guid CustomerId { get; }
    public string Admin { get; }
    public string Support { get; }
    public string TokenA => F.TokenA;
    public string TokenB => F.TokenB;
    public string Cs => InventoryEnv.Cs(F.Env);

    public static async Task<RefundEnv> StartAsync(PostgresFixture postgres)
    {
        CheckoutEnv f = await CheckoutEnv.StartAsync(postgres, new Dictionary<string, string>
        {
            ["Shipping:Carriers:0:Code"] = OrdersEnv.Carrier,
            ["Shipping:Carriers:0:Name"] = "Example Post",
            ["Shipping:Carriers:0:TrackingUrlTemplate"] = "https://tracking.example.com/{trackingNumber}",
        });
        await f.Env.AddUserAsync("admin@example.test", UserRole.Admin);
        await f.Env.AddUserAsync("support@example.test", UserRole.Support);
        Guid aId = await UserIdAsync(f, "a@example.test");
        return new RefundEnv(f, aId, await f.Env.AccessTokenAsync("admin@example.test"), await f.Env.AccessTokenAsync("support@example.test"));
    }

    private static Task<Guid> UserIdAsync(CheckoutEnv f, string email) => f.ScalarAsync<Guid>("SELECT id FROM users WHERE email = @e", ("e", email));

    public ValueTask DisposeAsync() => F.DisposeAsync();

    /// <summary>An order in the given status (owned by customer A) with a Succeeded payment of 25.00 and no refunds.</summary>
    public async Task<Guid> SeedOrderAsync(string status = "Paid")
    {
        Guid orderId = await OrderSeed.InsertAsync(Cs, CustomerId, status, Env.Clock.UtcNow);
        await F.Cart.ExecAsync(
            "INSERT INTO payments (id, order_id, attempt, provider, provider_payment_id, amount, currency, status, created_at, updated_at) " +
            "VALUES (@id, @o, 1, 'stripe', @pi, @amount, 'USD', 'Succeeded', @at, @at)",
            ("id", Guid.NewGuid()), ("o", orderId), ("pi", "pi_seed_" + Guid.NewGuid().ToString("N")), ("amount", Captured), ("at", Env.Clock.UtcNow));
        return orderId;
    }

    public Task<IdentityTestEnv.Api> RefundAsync(
        Guid orderId, string amount, string? key = "", string reason = "Damaged in transit", string? token = null, string currency = "USD")
    {
        string idem = key == "" ? "rf-" + Guid.NewGuid().ToString("N") : key!;
        object body = new { amount = new { amount, currency }, reason };
        return key is null
            ? Env.PostAsync($"/api/v1/admin/orders/{orderId}/refund", body, token ?? Admin)
            : Env.PostAsync($"/api/v1/admin/orders/{orderId}/refund", body, token ?? Admin, ("Idempotency-Key", idem));
    }

    public Task<IdentityTestEnv.Api> OrderActionAsync(Guid orderId, string action, object? body = null) =>
        Env.PostAsync($"/api/v1/admin/orders/{orderId}/{action}", body, Admin);

    public static Guid RefundIdOf(IdentityTestEnv.Api accepted) => accepted.Json.GetProperty("refundId").GetGuid();

    /// <summary>Posts a signed refund.updated event for the refund and expects 200.</summary>
    public async Task RefundWebhookAsync(Guid refundId, string status, string? eventId = null, string? providerRefundId = null)
    {
        IdentityTestEnv.Api r = await RefundWebhookRawAsync(refundId, status, eventId, providerRefundId);
        Assert.Equal(HttpStatusCode.OK, r.Status);
    }

    public Task<IdentityTestEnv.Api> RefundWebhookRawAsync(Guid refundId, string status, string? eventId = null, string? providerRefundId = null)
    {
        string evt = eventId ?? "evt_" + Guid.NewGuid().ToString("N");
        string provider = providerRefundId ?? "re_hook_" + refundId.ToString("N");
        string body = $"{{\"id\":\"{evt}\",\"type\":\"refund.updated\",\"data\":{{\"object\":{{\"id\":\"{provider}\",\"status\":\"{status}\",\"metadata\":{{\"refund_id\":\"{refundId}\"}}}}}}}}";
        return F.WebhookRawAsync(body, F.Sign(body, Env.Clock.UtcNow));
    }

    public Task<string> RefundStatusAsync(Guid refundId) => F.ScalarAsync<string>("SELECT status FROM refunds WHERE id = @r", ("r", refundId));

    public Task<string> PaymentStatusAsync(Guid orderId) =>
        F.ScalarAsync<string>("SELECT status FROM payments WHERE order_id = @o ORDER BY attempt DESC LIMIT 1", ("o", orderId));

    public Task<long> AuditCountAsync(string action, Guid? entity = null) =>
        entity is null
            ? F.CountAsync("SELECT count(*) FROM audit_log WHERE action = @a", ("a", action))
            : F.CountAsync("SELECT count(*) FROM audit_log WHERE action = @a AND entity_id = @e", ("a", action), ("e", entity.Value));

    public Task<long> HistoryCountAsync(Guid orderId, string toStatus) =>
        F.CountAsync("SELECT count(*) FROM order_status_history WHERE order_id = @o AND to_status = @s", ("o", orderId), ("s", toStatus));

    public Task<long> OpenRefundsCountAsync(Guid orderId) =>
        F.CountAsync("SELECT count(*) FROM refunds r JOIN payments p ON p.id = r.payment_id WHERE p.order_id = @o", ("o", orderId));

    public Task<decimal> CommittedAsync(Guid orderId) =>
        F.ScalarAsync<decimal>(
            "SELECT COALESCE(sum(r.amount), 0) FROM refunds r JOIN payments p ON p.id = r.payment_id WHERE p.order_id = @o AND r.status IN ('Pending','Succeeded')",
            ("o", orderId));

    public Task<Guid> VariantOfAsync(Guid orderId) =>
        F.ScalarAsync<Guid>("SELECT product_variant_id FROM order_items WHERE order_id = @o LIMIT 1", ("o", orderId));

    public Task<string> OrderTotalsAsync(Guid orderId) =>
        F.ScalarAsync<string>("SELECT subtotal || '/' || shipping_total || '/' || tax_total || '/' || grand_total FROM orders WHERE id = @o", ("o", orderId));
}
