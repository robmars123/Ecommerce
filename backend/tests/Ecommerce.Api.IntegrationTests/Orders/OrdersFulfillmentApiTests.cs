using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Identity.Domain;

namespace Ecommerce.Api.IntegrationTests.Orders;

// Test 30 (API half): illegal transitions are 409 with no history row; legal ones write one history and one audit row.
public class OrdersFulfillmentApiTests(PostgresFixture postgres)
{
    private const string HistorySql = "SELECT count(*) FROM order_status_history WHERE order_id = @id";
    private const string ShipmentSql = "SELECT count(*) FROM shipments WHERE order_id = @id";
    private const string AuditSql = "SELECT count(*) FROM audit_log WHERE entity_id = @id";

    private sealed record Fixture(IdentityTestEnv Env, string Cs, string Admin, Guid UserId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Env.DisposeAsync();

        public Task<Guid> OrderAsync(string status) => OrderSeed.InsertAsync(Cs, UserId, status, Env.Clock.UtcNow);

        public Task<IdentityTestEnv.Api> PostAsync(Guid id, string action, object? body = null, string? bearer = null) =>
            Env.PostAsync($"/api/v1/admin/orders/{id}/{action}", body, bearer ?? Admin);
    }

    private async Task<Fixture> StartAsync()
    {
        IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User customer = await env.AddUserAsync("customer@example.test");
        await env.AddUserAsync("admin@example.test", UserRole.Admin);
        return new Fixture(env, OrdersEnv.ConnectionString(env), await env.AccessTokenAsync("admin@example.test"), customer.Id);
    }

    private static readonly object Ship = new { carrierCode = OrdersEnv.Carrier, trackingNumber = "TRK-1" };

    [Theory]
    [InlineData("PendingPayment", "process")]
    [InlineData("Processing", "process")]
    [InlineData("Shipped", "process")]
    [InlineData("Delivered", "process")]
    [InlineData("Cancelled", "process")]
    [InlineData("Refunded", "process")]
    [InlineData("PendingPayment", "ship")]
    [InlineData("Paid", "ship")]
    [InlineData("Shipped", "ship")]
    [InlineData("Delivered", "ship")]
    [InlineData("Cancelled", "ship")]
    [InlineData("PendingPayment", "deliver")]
    [InlineData("Paid", "deliver")]
    [InlineData("Processing", "deliver")]
    [InlineData("Delivered", "deliver")]
    [InlineData("Returned", "deliver")]
    public async Task T30_an_action_from_a_status_that_does_not_allow_it_is_409_and_writes_nothing(string status, string action)
    {
        await using Fixture f = await StartAsync();
        Guid order = await f.OrderAsync(status);

        IdentityTestEnv.Api r = await f.PostAsync(order, action, action == "ship" ? Ship : null);

        Assert.Equal(HttpStatusCode.Conflict, r.Status);
        Assert.Equal(1, await OrderSeed.CountAsync(f.Cs, HistorySql, order));
        Assert.Equal(0, await OrderSeed.CountAsync(f.Cs, ShipmentSql, order));
        Assert.Equal(0, await OrderSeed.CountAsync(f.Cs, AuditSql, order));
    }

    [Theory]
    [InlineData("Paid", "Paid")]
    [InlineData("Paid", "Cancelled")]
    [InlineData("Paid", "Refunded")]
    [InlineData("Paid", "Shipped")]
    [InlineData("Paid", "Delivered")]
    [InlineData("PendingPayment", "Paid")]
    [InlineData("PendingPayment", "Cancelled")]
    [InlineData("Processing", "Shipped")]
    [InlineData("Processing", "Refunded")]
    [InlineData("Shipped", "Shipped")]
    [InlineData("Delivered", "Returned")]
    [InlineData("Cancelled", "Processing")]
    public async Task T30_an_override_to_anything_but_the_two_allowed_edges_is_409_and_writes_nothing(string from, string to)
    {
        await using Fixture f = await StartAsync();
        Guid order = await f.OrderAsync(from);
        IdentityTestEnv.Api r = await f.PostAsync(order, "status-override", new { toStatus = to, reason = "support asked" });

        Assert.Equal(HttpStatusCode.Conflict, r.Status);
        Assert.Equal(1, await OrderSeed.CountAsync(f.Cs, HistorySql, order));
        Assert.Equal(0, await OrderSeed.CountAsync(f.Cs, AuditSql, order));
    }

    [Fact]
    public async Task T30_process_ship_deliver_each_return_204_and_write_one_history_row_and_one_audit_row()
    {
        await using Fixture f = await StartAsync();
        Guid order = await f.OrderAsync("Paid");

        f.Env.Clock.UtcNow = f.Env.Clock.UtcNow.AddMinutes(1);
        Assert.Equal(HttpStatusCode.NoContent, (await f.PostAsync(order, "process")).Status);
        Assert.Equal(2, await OrderSeed.CountAsync(f.Cs, HistorySql, order));
        Assert.Equal(1, await OrderSeed.CountAsync(f.Cs, AuditSql, order));

        f.Env.Clock.UtcNow = f.Env.Clock.UtcNow.AddMinutes(1);
        Assert.Equal(HttpStatusCode.NoContent, (await f.PostAsync(order, "ship", new { carrierCode = OrdersEnv.Carrier, trackingNumber = "  TRK 9  " })).Status);
        Assert.Equal(3, await OrderSeed.CountAsync(f.Cs, HistorySql, order));
        Assert.Equal(1, await OrderSeed.CountAsync(f.Cs, ShipmentSql, order));
        Assert.Equal(2, await OrderSeed.CountAsync(f.Cs, AuditSql, order));

        f.Env.Clock.UtcNow = f.Env.Clock.UtcNow.AddMinutes(1);
        Assert.Equal(HttpStatusCode.NoContent, (await f.PostAsync(order, "deliver")).Status);
        Assert.Equal(4, await OrderSeed.CountAsync(f.Cs, HistorySql, order));
        Assert.Equal(3, await OrderSeed.CountAsync(f.Cs, AuditSql, order));

        string token = await f.Env.AccessTokenAsync("customer@example.test");
        JsonElement detail = (await f.Env.GetAsync($"/api/v1/orders/{order}", token)).Json;
        Assert.Equal("Delivered", detail.GetProperty("status").GetString());
        Assert.Equal(["Paid", "Processing", "Shipped", "Delivered"],
            detail.GetProperty("timeline").EnumerateArray().Select(t => t.GetProperty("toStatus").GetString()!).ToArray());
        JsonElement shipment = Assert.Single(detail.GetProperty("shipments").EnumerateArray());
        Assert.Equal("TRK 9", shipment.GetProperty("trackingNumber").GetString());
        Assert.NotEqual(JsonValueKind.Null, shipment.GetProperty("deliveredAt").ValueKind);
        Assert.Equal("25.00", detail.GetProperty("grandTotal").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task T30_an_override_paid_to_processing_with_a_reason_is_audited_with_actor_before_after_and_reason()
    {
        await using Fixture f = await StartAsync();
        Guid order = await f.OrderAsync("Paid");

        IdentityTestEnv.Api r = await f.PostAsync(order, "status-override", new { toStatus = "Processing", reason = "  warehouse started early  " });

        Assert.Equal(HttpStatusCode.NoContent, r.Status);
        Assert.Equal(2, await OrderSeed.CountAsync(f.Cs, HistorySql, order));
        await using Npgsql.NpgsqlDataSource source = Npgsql.NpgsqlDataSource.Create(f.Cs);
        await using Npgsql.NpgsqlCommand cmd = source.CreateCommand(
            "SELECT actor_id, action, entity_type, before_json::text, after_json::text, reason FROM audit_log WHERE entity_id = @id");
        cmd.Parameters.AddWithValue("id", order);
        await using Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.NotEqual(Guid.Empty, reader.GetGuid(0));
        Assert.NotEqual(f.UserId, reader.GetGuid(0));
        Assert.Equal("order.override", reader.GetString(1));
        Assert.Equal("order", reader.GetString(2));
        Assert.Contains("\"Paid\"", reader.GetString(3), StringComparison.Ordinal);
        Assert.Contains("\"Processing\"", reader.GetString(4), StringComparison.Ordinal);
        Assert.Equal("warehouse started early", reader.GetString(5));
    }

    [Fact]
    public async Task T30_an_override_shipped_to_delivered_is_allowed_and_marks_the_shipment_delivered()
    {
        await using Fixture f = await StartAsync();
        Guid order = await OrderSeed.InsertAsync(f.Cs, f.UserId, "Shipped", f.Env.Clock.UtcNow, shipment: new SeedShipment(OrdersEnv.Carrier, "T-1", f.Env.Clock.UtcNow));

        Assert.Equal(HttpStatusCode.NoContent, (await f.PostAsync(order, "status-override", new { toStatus = "Delivered", reason = "carrier confirmed by phone" })).Status);

        string token = await f.Env.AccessTokenAsync("customer@example.test");
        JsonElement shipment = Assert.Single((await f.Env.GetAsync($"/api/v1/orders/{order}", token)).Json.GetProperty("shipments").EnumerateArray());
        Assert.NotEqual(JsonValueKind.Null, shipment.GetProperty("deliveredAt").ValueKind);
    }

    [Fact]
    public async Task T30_two_parallel_ships_give_one_204_one_409_one_shipment_and_one_new_history_row()
    {
        await using Fixture f = await StartAsync();
        Guid order = await OrderSeed.InsertAsync(f.Cs, f.UserId, "Processing", f.Env.Clock.UtcNow);

        Task<IdentityTestEnv.Api>[] calls =
        [
            f.PostAsync(order, "ship", new { carrierCode = OrdersEnv.Carrier, trackingNumber = "RACE-1" }),
            f.PostAsync(order, "ship", new { carrierCode = OrdersEnv.Carrier, trackingNumber = "RACE-2" }),
        ];
        HttpStatusCode[] results = (await Task.WhenAll(calls)).Select(c => c.Status).Order().ToArray();

        Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.Conflict], results);
        Assert.Equal(1, await OrderSeed.CountAsync(f.Cs, ShipmentSql, order));
        Assert.Equal(2, await OrderSeed.CountAsync(f.Cs, HistorySql, order));
        Assert.Equal(1, await OrderSeed.CountAsync(f.Cs, AuditSql, order));
    }

    [Theory]
    [InlineData("process")]
    [InlineData("ship")]
    [InlineData("deliver")]
    [InlineData("status-override")]
    public async Task All_four_admin_actions_are_401_anonymous_and_403_for_customer_and_support(string action)
    {
        await using Fixture f = await StartAsync();
        await f.Env.AddUserAsync("support@example.test", UserRole.Support);
        string customer = await f.Env.AccessTokenAsync("customer@example.test");
        string support = await f.Env.AccessTokenAsync("support@example.test");
        Guid order = await f.OrderAsync("Paid");
        object body = new { };

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Env.PostAsync($"/api/v1/admin/orders/{order}/{action}", body)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.PostAsync(order, action, body, customer)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.PostAsync(order, action, body, support)).Status);
        Assert.Equal(1, await OrderSeed.CountAsync(f.Cs, HistorySql, order));
    }

    [Fact]
    public async Task An_unknown_order_is_404_for_each_admin_action()
    {
        await using Fixture f = await StartAsync();
        Guid missing = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await f.PostAsync(missing, "process")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await f.PostAsync(missing, "ship", Ship)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await f.PostAsync(missing, "deliver")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await f.PostAsync(missing, "status-override", new { toStatus = "Processing", reason = "x" })).Status);
    }

    [Theory]
    [InlineData("""{"carrierCode":"nope","trackingNumber":"T-1"}""")]
    [InlineData("""{"carrierCode":"example-post","trackingNumber":""}""")]
    [InlineData("""{"carrierCode":"example-post","trackingNumber":"   "}""")]
    [InlineData("""{"carrierCode":"example-post","trackingNumber":"TRK\u0007BEL"}""")]
    [InlineData("""{"carrierCode":"example-post"}""")]
    [InlineData("""{"trackingNumber":"T-1"}""")]
    [InlineData("""{"carrierCode":"example-post","trackingNumber":"T-1","status":"Delivered"}""")]
    public async Task Ship_rejects_bad_bodies_with_400_and_changes_nothing(string body)
    {
        await using Fixture f = await StartAsync();
        Guid order = await f.OrderAsync("Processing");

        Assert.Equal(HttpStatusCode.BadRequest, (await f.PostAsync(order, "ship", body)).Status);
        Assert.Equal(0, await OrderSeed.CountAsync(f.Cs, ShipmentSql, order));
        Assert.Equal(1, await OrderSeed.CountAsync(f.Cs, HistorySql, order));
    }

    [Fact]
    public async Task Ship_accepts_a_64_character_tracking_number_and_rejects_65()
    {
        await using Fixture f = await StartAsync();
        Guid tooLong = await f.OrderAsync("Processing");
        Guid justRight = await f.OrderAsync("Processing");

        Assert.Equal(HttpStatusCode.BadRequest, (await f.PostAsync(tooLong, "ship", new { carrierCode = OrdersEnv.Carrier, trackingNumber = new string('A', 65) })).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await f.PostAsync(justRight, "ship", new { carrierCode = OrdersEnv.Carrier, trackingNumber = new string('A', 64) })).Status);
    }

    [Theory]
    [InlineData("""{"toStatus":"Processing"}""")]
    [InlineData("""{"toStatus":"Processing","reason":""}""")]
    [InlineData("""{"toStatus":"Processing","reason":"   "}""")]
    [InlineData("""{"toStatus":"Bogus","reason":"why"}""")]
    [InlineData("""{"toStatus":"3","reason":"why"}""")]
    [InlineData("""{"reason":"why"}""")]
    [InlineData("""{"toStatus":"Processing","reason":"why","version":4}""")]
    public async Task Override_rejects_bad_bodies_with_400_and_changes_nothing(string body)
    {
        await using Fixture f = await StartAsync();
        Guid order = await f.OrderAsync("Paid");

        Assert.Equal(HttpStatusCode.BadRequest, (await f.PostAsync(order, "status-override", body)).Status);
        Assert.Equal(1, await OrderSeed.CountAsync(f.Cs, HistorySql, order));
        Assert.Equal(0, await OrderSeed.CountAsync(f.Cs, AuditSql, order));
    }

    [Fact]
    public async Task Override_rejects_a_501_character_reason()
    {
        await using Fixture f = await StartAsync();
        Guid order = await f.OrderAsync("Paid");

        Assert.Equal(HttpStatusCode.BadRequest, (await f.PostAsync(order, "status-override", new { toStatus = "Processing", reason = new string('r', 501) })).Status);
    }

    [Fact]
    public async Task Ship_is_400_when_no_carriers_are_configured()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        User customer = await env.AddUserAsync("customer@example.test");
        await env.AddUserAsync("admin@example.test", UserRole.Admin);
        string admin = await env.AccessTokenAsync("admin@example.test");
        Guid order = await OrderSeed.InsertAsync(OrdersEnv.ConnectionString(env), customer.Id, "Processing", env.Clock.UtcNow);

        Assert.Equal(HttpStatusCode.BadRequest, (await env.PostAsync($"/api/v1/admin/orders/{order}/ship", Ship, admin)).Status);
    }
}
