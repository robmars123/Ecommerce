using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Api.IntegrationTests.Orders;

internal static class OrdersEnv
{
    public const string Carrier = "example-post";

    public static Task<IdentityTestEnv> StartAsync(PostgresFixture postgres) =>
        IdentityTestEnv.StartAsync(postgres, settings: new Dictionary<string, string>
        {
            ["Shipping:Carriers:0:Code"] = Carrier,
            ["Shipping:Carriers:0:Name"] = "Example Post",
            ["Shipping:Carriers:0:TrackingUrlTemplate"] = "https://tracking.example.com/{trackingNumber}",
        });

    public static string ConnectionString(IdentityTestEnv env) => env.Db.Database.GetConnectionString()!;
}

// Test 7 (read part) and Test 29 (partial: no payment status until Slice 4).
public class OrdersReadApiTests(PostgresFixture postgres)
{
    [Fact]
    public async Task T7_another_customers_order_is_403_and_never_appears_in_the_callers_list()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        User b = await env.AddUserAsync("b@example.test");
        string tokenA = await env.AccessTokenAsync("a@example.test");
        string cs = OrdersEnv.ConnectionString(env);
        Guid orderA = await OrderSeed.InsertAsync(cs, a.Id, "Paid", env.Clock.UtcNow);
        Guid orderB = await OrderSeed.InsertAsync(cs, b.Id, "Paid", env.Clock.UtcNow);

        IdentityTestEnv.Api other = await env.GetAsync($"/api/v1/orders/{orderB}", tokenA);
        Assert.Equal(HttpStatusCode.Forbidden, other.Status);
        Assert.DoesNotContain("Test Buyer", other.Text);

        Assert.Equal(HttpStatusCode.OK, (await env.GetAsync($"/api/v1/orders/{orderA}", tokenA)).Status);

        IdentityTestEnv.Api list = await env.GetAsync("/api/v1/orders", tokenA);
        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Equal(1, list.Json.GetProperty("totalCount").GetInt32());
        Assert.Equal(orderA, list.Json.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.DoesNotContain(orderB.ToString(), list.Text);
    }

    [Fact]
    public async Task T7_anonymous_is_401_an_unknown_order_is_404_and_a_non_guid_id_is_404()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");

        Assert.Equal(HttpStatusCode.Unauthorized, (await env.GetAsync("/api/v1/orders")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.GetAsync($"/api/v1/orders/{Guid.NewGuid()}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await env.GetAsync($"/api/v1/orders/{Guid.NewGuid()}", token)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await env.GetAsync("/api/v1/orders/not-a-guid", token)).Status);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task List_rejects_out_of_range_paging_with_400(string query)
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");

        Assert.Equal(HttpStatusCode.BadRequest, (await env.GetAsync($"/api/v1/orders?{query}", token)).Status);
    }

    [Fact]
    public async Task T29_list_is_newest_first_paginated_and_past_the_last_page_is_empty_with_the_real_total()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");
        string cs = OrdersEnv.ConnectionString(env);
        DateTimeOffset t0 = env.Clock.UtcNow;
        Guid oldest = await OrderSeed.InsertAsync(cs, a.Id, "Paid", t0.AddHours(-3), orderNumber: "ORD-2026-000001");
        Guid middle = await OrderSeed.InsertAsync(cs, a.Id, "Paid", t0.AddHours(-2), orderNumber: "ORD-2026-000002");
        Guid newest = await OrderSeed.InsertAsync(cs, a.Id, "Paid", t0.AddHours(-1), orderNumber: "ORD-2026-000003");

        IdentityTestEnv.Api first = await env.GetAsync("/api/v1/orders?page=1&pageSize=2", token);
        JsonElement items = first.Json.GetProperty("items");
        Assert.Equal([newest, middle], items.EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray());
        Assert.Equal(3, first.Json.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, first.Json.GetProperty("totalPages").GetInt32());
        Assert.Equal("ORD-2026-000003", items[0].GetProperty("orderNumber").GetString());
        Assert.Equal("Paid", items[0].GetProperty("status").GetString());
        Assert.Equal("25.00", items[0].GetProperty("grandTotal").GetProperty("amount").GetString());
        Assert.Equal("USD", items[0].GetProperty("grandTotal").GetProperty("currency").GetString());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("trackingUrl").ValueKind);

        IdentityTestEnv.Api second = await env.GetAsync("/api/v1/orders?page=2&pageSize=2", token);
        Assert.Equal([oldest], second.Json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray());

        IdentityTestEnv.Api past = await env.GetAsync("/api/v1/orders?page=9&pageSize=2", token);
        Assert.Equal(HttpStatusCode.OK, past.Status);
        Assert.Empty(past.Json.GetProperty("items").EnumerateArray());
        Assert.Equal(3, past.Json.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task T29_detail_has_snapshots_address_method_ordered_timeline_and_a_url_encoded_tracking_link_with_no_actor_id()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");
        string cs = OrdersEnv.ConnectionString(env);
        DateTimeOffset t0 = env.Clock.UtcNow.AddDays(-3);
        Guid adminId = Guid.NewGuid();
        Guid order = await OrderSeed.InsertAsync(
            cs, a.Id, "Shipped", t0,
            items: [new SeedItem("TEE-BLK-M", "Black tee, M", 20.00m, 2), new SeedItem("CAP-RED", "Red cap", 12.50m, 1)],
            // Inserted out of order on purpose: the API must sort the timeline ascending by time.
            history:
            [
                new SeedHistory("Paid", "Processing", t0.AddHours(2), null, adminId),
                new SeedHistory(null, "PendingPayment", t0),
                new SeedHistory("Processing", "Shipped", t0.AddHours(3), null, adminId),
                new SeedHistory("PendingPayment", "Paid", t0.AddHours(1)),
            ],
            shipment: new SeedShipment(OrdersEnv.Carrier, "AB 12/3", t0.AddHours(3)));

        IdentityTestEnv.Api r = await env.GetAsync($"/api/v1/orders/{order}", token);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        JsonElement json = r.Json;
        Assert.Equal("Shipped", json.GetProperty("status").GetString());
        JsonElement items = json.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        JsonElement tee = items.EnumerateArray().Single(i => i.GetProperty("sku").GetString() == "TEE-BLK-M");
        Assert.Equal("Black tee, M", tee.GetProperty("name").GetString());
        Assert.Equal("20.00", tee.GetProperty("unitPrice").GetProperty("amount").GetString());
        Assert.Equal(2, tee.GetProperty("quantity").GetInt32());
        Assert.Equal("40.00", tee.GetProperty("lineTotal").GetProperty("amount").GetString());
        Assert.Equal("52.50", json.GetProperty("subtotal").GetProperty("amount").GetString());
        Assert.Equal("0.00", json.GetProperty("discountTotal").GetProperty("amount").GetString());
        Assert.Equal("5.00", json.GetProperty("shippingTotal").GetProperty("amount").GetString());
        Assert.Equal("0.00", json.GetProperty("taxTotal").GetProperty("amount").GetString());
        Assert.Equal("57.50", json.GetProperty("grandTotal").GetProperty("amount").GetString());
        Assert.Equal("Standard", json.GetProperty("shippingMethod").GetProperty("name").GetString());
        Assert.Equal("standard", json.GetProperty("shippingMethod").GetProperty("code").GetString());
        Assert.Equal("1 Example Street", json.GetProperty("shippingAddress").GetProperty("line1").GetString());
        Assert.Equal("US", json.GetProperty("shippingAddress").GetProperty("country").GetString());

        string[] timeline = json.GetProperty("timeline").EnumerateArray().Select(t => t.GetProperty("toStatus").GetString()!).ToArray();
        Assert.Equal(["PendingPayment", "Paid", "Processing", "Shipped"], timeline);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("timeline")[0].GetProperty("fromStatus").ValueKind);

        JsonElement shipment = Assert.Single(json.GetProperty("shipments").EnumerateArray());
        Assert.Equal("Example Post", shipment.GetProperty("carrierName").GetString());
        Assert.Equal("AB 12/3", shipment.GetProperty("trackingNumber").GetString());
        Assert.Equal("https://tracking.example.com/AB%2012%2F3", shipment.GetProperty("trackingUrl").GetString());
        Assert.Equal(JsonValueKind.Null, shipment.GetProperty("deliveredAt").ValueKind);

        Assert.DoesNotContain(adminId.ToString(), r.Text);
        Assert.DoesNotContain(a.Id.ToString(), r.Text);
        Assert.DoesNotContain("changedBy", r.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userId", r.Text, StringComparison.OrdinalIgnoreCase);

        IdentityTestEnv.Api list = await env.GetAsync("/api/v1/orders", token);
        Assert.Equal("https://tracking.example.com/AB%2012%2F3", list.Json.GetProperty("items")[0].GetProperty("trackingUrl").GetString());
    }

    [Fact]
    public async Task T29_a_shipment_whose_carrier_is_no_longer_configured_shows_the_code_and_no_link()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");
        Guid order = await OrderSeed.InsertAsync(
            OrdersEnv.ConnectionString(env), a.Id, "Shipped", env.Clock.UtcNow,
            shipment: new SeedShipment("retired-carrier", "T-1", env.Clock.UtcNow));

        JsonElement shipment = Assert.Single((await env.GetAsync($"/api/v1/orders/{order}", token)).Json.GetProperty("shipments").EnumerateArray());

        Assert.Equal("retired-carrier", shipment.GetProperty("carrierName").GetString());
        Assert.Equal(JsonValueKind.Null, shipment.GetProperty("trackingUrl").ValueKind);
    }
}
