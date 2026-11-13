using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Catalog.Domain;
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
            ["Catalog:Images:BaseUrl"] = "https://img.example.test",
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

    private const string ExpiredPill = "Payment not completed in time";

    private static IReadOnlyList<SeedHistory> ExpiredHistory(DateTimeOffset t0) =>
    [
        new SeedHistory(null, "PendingPayment", t0),
        new SeedHistory("PendingPayment", "Cancelled", t0.AddMinutes(16), "reservation-expired"),
    ];

    [Fact]
    public async Task P2_50_an_expired_order_returns_the_cancellation_alert_and_pill_note()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");
        Guid order = await OrderSeed.InsertAsync(
            OrdersEnv.ConnectionString(env), a.Id, "Cancelled", env.Clock.UtcNow, history: ExpiredHistory(env.Clock.UtcNow));

        IdentityTestEnv.Api r = await env.GetAsync($"/api/v1/orders/{order}", token);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        JsonElement note = r.Json.GetProperty("statusNote");
        Assert.Equal(ExpiredPill, note.GetProperty("pillNote").GetString());
        Assert.Equal("This order was cancelled", note.GetProperty("alert").GetProperty("title").GetString());
        Assert.Equal(
            "We held your items while you paid, but payment was not completed before the hold ran out, so we released them and cancelled the order. Nothing will ship. If a payment went through after that, we refund it automatically.",
            note.GetProperty("alert").GetProperty("message").GetString());
    }

    [Fact]
    public async Task P2_50_a_refunded_order_returns_a_pill_note_and_a_null_alert()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");
        Guid order = await OrderSeed.InsertAsync(OrdersEnv.ConnectionString(env), a.Id, "Refunded", env.Clock.UtcNow);

        JsonElement note = (await env.GetAsync($"/api/v1/orders/{order}", token)).Json.GetProperty("statusNote");

        Assert.Equal("Refunded in full to your original payment method", note.GetProperty("pillNote").GetString());
        Assert.Equal(JsonValueKind.Null, note.GetProperty("alert").ValueKind);
    }

    [Fact]
    public async Task P2_50_a_paid_order_has_a_null_status_note()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");
        Guid order = await OrderSeed.InsertAsync(OrdersEnv.ConnectionString(env), a.Id, "Paid", env.Clock.UtcNow);

        Assert.Equal(JsonValueKind.Null, (await env.GetAsync($"/api/v1/orders/{order}", token)).Json.GetProperty("statusNote").ValueKind);
    }

    [Fact]
    public async Task P2_50_another_customers_cancelled_order_is_403_without_the_note()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        User b = await env.AddUserAsync("b@example.test");
        string tokenA = await env.AccessTokenAsync("a@example.test");
        Guid orderB = await OrderSeed.InsertAsync(
            OrdersEnv.ConnectionString(env), b.Id, "Cancelled", env.Clock.UtcNow, history: ExpiredHistory(env.Clock.UtcNow));

        IdentityTestEnv.Api r = await env.GetAsync($"/api/v1/orders/{orderB}", tokenA);

        Assert.Equal(HttpStatusCode.Forbidden, r.Status);
        Assert.DoesNotContain(ExpiredPill, r.Text);
        Assert.NotEqual(a.Id, b.Id);
    }

    private static string[] Descriptions(JsonElement json) =>
        json.GetProperty("timeline").EnumerateArray().Select(t => t.GetProperty("description").GetString()!).ToArray();

    [Fact]
    public async Task P2_53_each_timeline_entry_has_its_description_in_order()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");
        DateTimeOffset t0 = env.Clock.UtcNow;
        Guid order = await OrderSeed.InsertAsync(
            OrdersEnv.ConnectionString(env), a.Id, "Shipped", t0,
            history:
            [
                new SeedHistory(null, "PendingPayment", t0),
                new SeedHistory("PendingPayment", "Paid", t0.AddMinutes(1)),
                new SeedHistory("Paid", "Processing", t0.AddMinutes(2)),
                new SeedHistory("Processing", "Shipped", t0.AddMinutes(3)),
            ]);

        string[] texts = Descriptions((await env.GetAsync($"/api/v1/orders/{order}", token)).Json);

        Assert.Equal(
            [
                "We received your order and held your items for payment.",
                "Your payment was confirmed.",
                "We started preparing your order.",
                "Your order was handed to the carrier.",
            ],
            texts);
    }

    [Fact]
    public async Task P2_53_an_expired_cancellation_entry_is_described_as_expiry()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");
        Guid order = await OrderSeed.InsertAsync(
            OrdersEnv.ConnectionString(env), a.Id, "Cancelled", env.Clock.UtcNow, history: ExpiredHistory(env.Clock.UtcNow));

        string[] texts = Descriptions((await env.GetAsync($"/api/v1/orders/{order}", token)).Json);

        Assert.Equal(
            "Payment was not completed before the hold on your items ran out, so we released them and cancelled the order.",
            texts[^1]);
    }

    [Fact]
    public async Task P2_53_an_admin_override_reason_never_appears_in_a_description()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");
        DateTimeOffset t0 = env.Clock.UtcNow;
        Guid order = await OrderSeed.InsertAsync(
            OrdersEnv.ConnectionString(env), a.Id, "Processing", t0,
            history:
            [
                new SeedHistory(null, "PendingPayment", t0),
                new SeedHistory("PendingPayment", "Paid", t0.AddMinutes(1)),
                new SeedHistory("Paid", "Processing", t0.AddMinutes(2), "Internal: VIP, skip fraud check", Guid.NewGuid()),
            ]);

        string[] texts = Descriptions((await env.GetAsync($"/api/v1/orders/{order}", token)).Json);

        Assert.Equal("We started preparing your order.", texts[^1]);
        Assert.All(texts, t => Assert.DoesNotContain("VIP", t));
    }

    [Fact]
    public async Task P2_53_another_customers_order_is_403_without_descriptions()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        await env.AddUserAsync("a@example.test");
        User b = await env.AddUserAsync("b@example.test");
        string tokenA = await env.AccessTokenAsync("a@example.test");
        DateTimeOffset t0 = env.Clock.UtcNow;
        Guid orderB = await OrderSeed.InsertAsync(
            OrdersEnv.ConnectionString(env), b.Id, "Paid", t0,
            history: [new SeedHistory(null, "PendingPayment", t0), new SeedHistory("PendingPayment", "Paid", t0.AddMinutes(1))]);

        IdentityTestEnv.Api r = await env.GetAsync($"/api/v1/orders/{orderB}", tokenA);

        Assert.Equal(HttpStatusCode.Forbidden, r.Status);
        Assert.DoesNotContain("Your payment was confirmed.", r.Text);
    }

    private static async Task<Guid> CatalogVariantAsync(IdentityTestEnv env, string sku, string? colour, string? imageKey = null, string altText = "")
    {
        DateTimeOffset at = env.Clock.UtcNow;
        string key = sku.ToLowerInvariant();
        Category category = Category.Create(Guid.CreateVersion7(at), "cat-" + key, "Cat " + sku, null);
        Product product = Product.Create(Guid.CreateVersion7(at), "p-" + key, "Catalog name " + sku, "", category.Id, at);
        ProductVariant variant = product.AddVariant(Guid.CreateVersion7(at), sku, sku, null, colour, 10m, "USD", null, 100, at);
        if (imageKey is not null)
        {
            product.AddImage(Guid.CreateVersion7(at), imageKey, altText, 0, at);
        }
        product.Publish(at);
        env.Db.Add(category);
        env.Db.Add(product);
        await env.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return variant.Id;
    }

    [Fact]
    public async Task P2_51_an_item_returns_its_catalog_colour_and_primary_image_url()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");
        Guid variant = await CatalogVariantAsync(env, "TEE-BLK-M", "Black", "tees/black front.jpg", "Black tee, front");
        Guid order = await OrderSeed.InsertAsync(
            OrdersEnv.ConnectionString(env), a.Id, "Paid", env.Clock.UtcNow, items: [new SeedItem("TEE-BLK-M", "Black tee, M", 20.00m, 2, variant)]);

        IdentityTestEnv.Api r = await env.GetAsync($"/api/v1/orders/{order}", token);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        JsonElement item = r.Json.GetProperty("items")[0];
        Assert.Equal("Black", item.GetProperty("colour").GetString());
        Assert.Equal("https://img.example.test/tees/black%20front.jpg", item.GetProperty("image").GetProperty("url").GetString());
        Assert.Equal("Black tee, front", item.GetProperty("image").GetProperty("altText").GetString());
        Assert.Equal("Black tee, M", item.GetProperty("name").GetString());
    }

    [Fact]
    public async Task P2_51_missing_colour_image_or_catalog_variant_gives_nulls_and_still_200()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");
        Guid bare = await CatalogVariantAsync(env, "BARE-1", null);
        Guid order = await OrderSeed.InsertAsync(
            OrdersEnv.ConnectionString(env), a.Id, "Paid", env.Clock.UtcNow,
            items: [new SeedItem("BARE-1", "Bare, M", 20.00m, 2, bare), new SeedItem("GONE-1", "Gone", 12.50m, 1)]);

        IdentityTestEnv.Api r = await env.GetAsync($"/api/v1/orders/{order}", token);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        JsonElement items = r.Json.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        foreach (JsonElement item in items.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Null, item.GetProperty("image").ValueKind);
            Assert.Equal(JsonValueKind.Null, item.GetProperty("colour").ValueKind);
        }
        JsonElement bareItem = items.EnumerateArray().Single(i => i.GetProperty("sku").GetString() == "BARE-1");
        Assert.Equal("Bare, M", bareItem.GetProperty("name").GetString());
        Assert.Equal("20.00", bareItem.GetProperty("unitPrice").GetProperty("amount").GetString());
        Assert.Equal("40.00", bareItem.GetProperty("lineTotal").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task P2_51_items_keep_their_order_and_each_gets_its_own_variant_data()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        string token = await env.AccessTokenAsync("a@example.test");
        Guid black = await CatalogVariantAsync(env, "TEE-BLK", "Black", "tees/black.jpg", "Black tee");
        Guid red = await CatalogVariantAsync(env, "TEE-RED", "Red", "tees/red.jpg", "Red tee");
        Guid order = await OrderSeed.InsertAsync(
            OrdersEnv.ConnectionString(env), a.Id, "Paid", env.Clock.UtcNow,
            items: [new SeedItem("TEE-BLK", "Black tee", 20.00m, 1, black), new SeedItem("TEE-RED", "Red tee", 20.00m, 1, red)]);

        JsonElement items = (await env.GetAsync($"/api/v1/orders/{order}", token)).Json.GetProperty("items");

        foreach (JsonElement item in items.EnumerateArray())
        {
            bool isBlack = item.GetProperty("variantId").GetGuid() == black;
            Assert.Equal(isBlack ? "Black" : "Red", item.GetProperty("colour").GetString());
            Assert.Equal(isBlack ? "https://img.example.test/tees/black.jpg" : "https://img.example.test/tees/red.jpg", item.GetProperty("image").GetProperty("url").GetString());
            Assert.Equal(isBlack ? "TEE-BLK" : "TEE-RED", item.GetProperty("sku").GetString());
        }
    }

    [Fact]
    public async Task P2_51_another_customers_order_is_403_without_item_data()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        await env.AddUserAsync("a@example.test");
        User b = await env.AddUserAsync("b@example.test");
        string tokenA = await env.AccessTokenAsync("a@example.test");
        Guid variant = await CatalogVariantAsync(env, "TEE-BLK-M", "Black", "tees/black.jpg", "Black tee");
        Guid orderB = await OrderSeed.InsertAsync(
            OrdersEnv.ConnectionString(env), b.Id, "Paid", env.Clock.UtcNow, items: [new SeedItem("TEE-BLK-M", "Black tee, M", 20.00m, 1, variant)]);

        IdentityTestEnv.Api r = await env.GetAsync($"/api/v1/orders/{orderB}", tokenA);

        Assert.Equal(HttpStatusCode.Forbidden, r.Status);
        Assert.DoesNotContain("img.example.test", r.Text);
        Assert.DoesNotContain("Black", r.Text);
    }

    [Fact]
    public async Task P2_51_admin_order_detail_embeds_the_item_colour_and_image()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User a = await env.AddUserAsync("a@example.test");
        await env.AddUserAsync("admin@example.test", UserRole.Admin);
        string admin = await env.AccessTokenAsync("admin@example.test");
        Guid variant = await CatalogVariantAsync(env, "TEE-BLK-M", "Black", "tees/black front.jpg", "Black tee, front");
        Guid order = await OrderSeed.InsertAsync(
            OrdersEnv.ConnectionString(env), a.Id, "Paid", env.Clock.UtcNow, items: [new SeedItem("TEE-BLK-M", "Black tee, M", 20.00m, 2, variant)]);

        IdentityTestEnv.Api r = await env.GetAsync($"/api/v1/admin/orders/{order}", admin);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        JsonElement item = r.Json.GetProperty("order").GetProperty("items")[0];
        Assert.Equal("Black", item.GetProperty("colour").GetString());
        Assert.Equal("https://img.example.test/tees/black%20front.jpg", item.GetProperty("image").GetProperty("url").GetString());
    }
}
