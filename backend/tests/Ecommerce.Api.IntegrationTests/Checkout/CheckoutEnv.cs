using System.Net;
using System.Security.Cryptography;
using System.Text;
using Ecommerce.Api.IntegrationTests.Cart;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Inventory;
using Ecommerce.Inventory.Application;
using Ecommerce.Payments.Application;
using Ecommerce.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Ecommerce.Api.IntegrationTests.Checkout;

/// <summary>
/// Slice 4 fixture: two customers and a variant helper (CartEnv), shipping configuration, the fake payment gateway, signed
/// webhook posting and the expiry sweep. The real gateway is replaced, so nothing here can reach the payment provider.
/// </summary>
internal sealed class CheckoutEnv : IAsyncDisposable
{
    public const string Method = "standard";
    public const decimal ShippingAmount = 5.00m;

    private CheckoutEnv(CartEnv cart, FakeGatewayState gateway)
    {
        Cart = cart;
        Gateway = gateway;
    }

    public CartEnv Cart { get; }
    public FakeGatewayState Gateway { get; }
    public IdentityTestEnv Env => Cart.Env;
    public string TokenA => Cart.TokenA;
    public string TokenB => Cart.TokenB;

    public static async Task<CheckoutEnv> StartAsync(
        PostgresFixture postgres, IReadOnlyDictionary<string, string>? settings = null, Action<IServiceCollection>? configure = null)
    {
        FakeGatewayState gateway = new();
        Dictionary<string, string> all = new()
        {
            ["Shipping:Methods:0:Code"] = Method,
            ["Shipping:Methods:0:Name"] = "Standard",
            ["Shipping:Methods:0:Amount"] = "5.00",
            ["Shipping:Methods:0:EstimatedDays"] = "5",
            ["Shipping:Countries:0"] = "US",
        };
        foreach ((string key, string value) in settings ?? new Dictionary<string, string>())
        {
            all[key] = value;
        }
        CartEnv cart = await CartEnv.StartAsync(postgres, all, services =>
        {
            services.AddSingleton(gateway);
            services.RemoveAll<IPaymentGateway>();
            services.AddScoped<IPaymentGateway>(sp => new FakePaymentGateway(gateway, sp.GetRequiredService<EcommerceDbContext>()));
            configure?.Invoke(services);
        });
        return new CheckoutEnv(cart, gateway);
    }

    public ValueTask DisposeAsync() => Cart.DisposeAsync();

    public static object Body(string method = Method, string country = "US") => new
    {
        shippingAddress = new { fullName = "Test Buyer", line1 = "1 Example Street", city = "Exampleville", postalCode = "12345", country },
        shippingMethodCode = method,
    };

    public Task<IdentityTestEnv.Api> CheckoutAsync(string? token, string? key, object? body = null) =>
        key is null
            ? Env.PostAsync("/api/v1/checkout", body ?? Body(), token)
            : Env.PostAsync("/api/v1/checkout", body ?? Body(), token, ("Idempotency-Key", key));

    public Task<IdentityTestEnv.Api> RetryAsync(string? token, Guid orderId, string? key) =>
        key is null
            ? Env.PostAsync($"/api/v1/orders/{orderId}/retry-payment", new { }, token)
            : Env.PostAsync($"/api/v1/orders/{orderId}/retry-payment", new { }, token, ("Idempotency-Key", key));

    /// <summary>Adds the variant to user A's cart (the server cart) and checks out with the given key; the order must be created.</summary>
    public async Task<Guid> PlaceOrderAsync(Guid variant, int quantity, string key, string? token = null)
    {
        IdentityTestEnv.Api added = await Cart.AddAsync(token ?? TokenA, variant, quantity);
        Assert.Equal(HttpStatusCode.OK, added.Status);
        IdentityTestEnv.Api placed = await CheckoutAsync(token ?? TokenA, key);
        Assert.Equal(HttpStatusCode.OK, placed.Status);
        return placed.Json.GetProperty("order").GetProperty("id").GetGuid();
    }

    // ---- webhook ----

    public static string EventBody(string eventId, string type, string intentId, long amountReceived, string currency) =>
        $"{{\"id\":\"{eventId}\",\"type\":\"{type}\",\"data\":{{\"object\":{{\"id\":\"{intentId}\",\"amount_received\":{amountReceived},\"currency\":\"{currency}\"}}}}}}";

    /// <summary>The event body; amount and currency default to the payment row of that intent (what a faithful provider reports).</summary>
    public async Task<string> BodyAsync(string eventId, string type, string intentId, long? amountReceived = null, string? currency = null)
    {
        long amount = amountReceived ?? await ScalarOrAsync("SELECT (amount * 100)::bigint FROM payments WHERE provider_payment_id = @i", 0L, ("i", intentId));
        string cur = currency ?? (await ScalarOrAsync("SELECT lower(currency) FROM payments WHERE provider_payment_id = @i", "usd", ("i", intentId)));
        return EventBody(eventId, type, intentId, amount, cur);
    }

    private async Task<T> ScalarOrAsync<T>(string sql, T fallback, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(InventoryEnv.Cs(Env));
        await using NpgsqlCommand cmd = source.CreateCommand(sql);
        foreach ((string name, object value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        object? scalar = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return scalar is null ? fallback : (T)scalar;
    }

    /// <summary>The provider's scheme: <c>t=timestamp,v1=hex(HMAC-SHA256(secret, "t.body"))</c>, signed with this host's random secret.</summary>
    public string Sign(string body, DateTimeOffset at, string? secret = null)
    {
        long t = at.ToUnixTimeSeconds();
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(secret ?? Env.Factory.WebhookSecret));
        string v1 = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{t}.{body}")));
        return $"t={t},v1={v1}";
    }

    public Task<IdentityTestEnv.Api> WebhookRawAsync(string body, string? signature) =>
        signature is null
            ? Env.SendAsync(HttpMethod.Post, "/api/v1/webhooks/stripe", body)
            : Env.SendAsync(HttpMethod.Post, "/api/v1/webhooks/stripe", body, null, ("Stripe-Signature", signature));

    public async Task<IdentityTestEnv.Api> WebhookAsync(
        string eventId, string intentId, string type = "payment_intent.succeeded", long? amountReceived = null, string? currency = null)
    {
        string body = await BodyAsync(eventId, type, intentId, amountReceived, currency);
        return await WebhookRawAsync(body, Sign(body, Env.Clock.UtcNow));
    }

    public async Task<string> IntentOfAsync(Guid orderId, int attempt = 0) =>
        await ScalarAsync<string>(
            attempt == 0
                ? "SELECT provider_payment_id FROM payments WHERE order_id = @o ORDER BY attempt DESC LIMIT 1"
                : "SELECT provider_payment_id FROM payments WHERE order_id = @o AND attempt = " + attempt,
            ("o", orderId));

    public async Task<Guid> PaymentIdAsync(Guid orderId, int attempt) =>
        await ScalarAsync<Guid>("SELECT id FROM payments WHERE order_id = @o AND attempt = @a", ("o", orderId), ("a", attempt));

    /// <summary>Posts payment_intent.succeeded for the order's latest intent and expects 200.</summary>
    public async Task PayAsync(Guid orderId, string eventId)
    {
        IdentityTestEnv.Api r = await WebhookAsync(eventId, await IntentOfAsync(orderId));
        Assert.Equal(HttpStatusCode.OK, r.Status);
    }

    // ---- expiry ----

    public async Task<int> SweepAsync()
    {
        using IServiceScope scope = Env.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReservationExpiryService>().SweepAsync(TestContext.Current.CancellationToken);
    }

    // ---- reads ----

    public Task<long> CountAsync(string sql, params (string Name, object Value)[] parameters) =>
        ScalarAsync<long>(sql, parameters);

    public async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(InventoryEnv.Cs(Env));
        await using NpgsqlCommand cmd = source.CreateCommand(sql);
        foreach ((string name, object value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        object? scalar = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        // ExecuteScalar hands a timestamptz back as a UTC DateTime.
        return scalar is DateTime utc && typeof(T) == typeof(DateTimeOffset)
            ? (T)(object)new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc))
            : (T)scalar!;
    }

    public Task<string> OrderStatusAsync(Guid orderId) =>
        ScalarAsync<string>("SELECT status FROM orders WHERE id = @o", ("o", orderId));

    public Task<long> OutboxCountAsync(string eventType) =>
        CountAsync("SELECT count(*) FROM outbox_events WHERE event_type = @t", ("t", eventType));

    public Task<IdentityTestEnv.Api> OrderAsync(string token, Guid orderId) =>
        Env.GetAsync($"/api/v1/orders/{orderId}", token);
}
