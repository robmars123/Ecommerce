using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Inventory;
using Ecommerce.Api.IntegrationTests.Orders;
using Ecommerce.Identity.Domain;

namespace Ecommerce.Api.IntegrationTests.Admin;

// Slice P2.8: GET /api/v1/admin/search?q= (arch 13.2 "Admin search UI and contract").
public class AdminSearchApiTests(PostgresFixture postgres)
{
    private const string Url = "/api/v1/admin/search";
    private static readonly DateTimeOffset Day = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed class Fixture(IdentityTestEnv env, string cs, string admin, string support, string customer, Guid buyerId) : IAsyncDisposable
    {
        public IdentityTestEnv Env { get; } = env;
        public string Cs { get; } = cs;
        public string Admin { get; } = admin;
        public string Support { get; } = support;
        public string Customer { get; } = customer;
        public Guid BuyerId { get; } = buyerId;

        public Task<IdentityTestEnv.Api> SearchAsync(string rawQuery, string? token = null) => Env.GetAsync(Url + "?q=" + rawQuery, token ?? Admin);

        public Task<Guid> OrderAsync(string number, DateTimeOffset? at = null, string status = "Paid") =>
            OrderSeed.InsertAsync(Cs, BuyerId, status, at ?? Day, null, null, null, number);

        public ValueTask DisposeAsync() => Env.DisposeAsync();
    }

    private async Task<Fixture> StartAsync()
    {
        IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        await env.AddUserAsync("admin@example.test", UserRole.Admin);
        await env.AddUserAsync("support@example.test", UserRole.Support);
        User buyer = await env.AddUserAsync("customer@example.test");
        return new Fixture(
            env, OrdersEnv.ConnectionString(env),
            await env.AccessTokenAsync("admin@example.test"), await env.AccessTokenAsync("support@example.test"), await env.AccessTokenAsync("customer@example.test"), buyer.Id);
    }

    private static List<string> Names(IdentityTestEnv.Api r, string group, string property) =>
        r.Json.GetProperty(group).EnumerateArray().Select(i => i.GetProperty(property).GetString()!).ToList();

    private static List<string> PropertyNames(JsonElement e) => e.EnumerateObject().Select(p => p.Name).Order().ToList();

    [Fact]
    public async Task I1_Anonymous_gets_401()
    {
        await using Fixture f = await StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Env.GetAsync(Url + "?q=ord")).Status);
    }

    [Fact]
    public async Task I2_Customer_gets_403_and_staff_get_200()
    {
        await using Fixture f = await StartAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await f.SearchAsync("ord", f.Customer)).Status);
        Assert.Equal(HttpStatusCode.OK, (await f.SearchAsync("ord", f.Admin)).Status);
        Assert.Equal(HttpStatusCode.OK, (await f.SearchAsync("ord", f.Support)).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?q=a")]
    [InlineData("?q=%20%20")]
    [InlineData("?q=%20a%20")]
    [InlineData("?q=")]
    public async Task I3_Bad_q_gets_400_with_a_q_error(string suffix)
    {
        await using Fixture f = await StartAsync();

        IdentityTestEnv.Api r = await f.Env.GetAsync(Url + suffix, f.Admin);

        Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        Assert.True(r.Json.GetProperty("errors").TryGetProperty("q", out _));
    }

    [Fact]
    public async Task I3_101_characters_gets_400_and_100_gets_200()
    {
        await using Fixture f = await StartAsync();

        IdentityTestEnv.Api tooLong = await f.SearchAsync(new string('x', 101));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.Status);
        Assert.True(tooLong.Json.GetProperty("errors").TryGetProperty("q", out _));
        Assert.Equal(HttpStatusCode.OK, (await f.SearchAsync(new string('x', 100))).Status);
    }

    [Fact]
    public async Task I4_Admin_finds_orders_products_and_skus_with_the_contract_shape()
    {
        await using Fixture f = await StartAsync();
        Guid orderId = await f.OrderAsync("ORD-2026-000777");
        await f.OrderAsync("ORD-2026-555111");
        await InventoryEnv.AddProductAsync(f.Env, "i4-mug", "Blue Mug", ["MUG-BLU-01"]);
        await InventoryEnv.AddProductAsync(f.Env, "i4-hat", "Red Hat", ["HAT-RED-01"]);

        IdentityTestEnv.Api byMug = await f.SearchAsync("mug");
        Assert.Equal(HttpStatusCode.OK, byMug.Status);
        JsonElement product = Assert.Single(byMug.Json.GetProperty("products").EnumerateArray());
        JsonElement sku = Assert.Single(byMug.Json.GetProperty("skus").EnumerateArray());
        Assert.Equal("Blue Mug", product.GetProperty("name").GetString());
        Guid productId = product.GetProperty("id").GetGuid();
        Assert.Equal("MUG-BLU-01", sku.GetProperty("sku").GetString());
        Assert.Equal(productId, sku.GetProperty("productId").GetGuid());
        Assert.Equal("Blue Mug", sku.GetProperty("productName").GetString());
        Assert.Empty(byMug.Json.GetProperty("orders").EnumerateArray());

        IdentityTestEnv.Api byNumber = await f.SearchAsync("000777");
        JsonElement order = Assert.Single(byNumber.Json.GetProperty("orders").EnumerateArray());
        Assert.Equal(orderId, order.GetProperty("id").GetGuid());
        Assert.Equal("ORD-2026-000777", order.GetProperty("orderNumber").GetString());
        Assert.Equal("Paid", order.GetProperty("status").GetString());
    }

    [Fact]
    public async Task I5_Inactive_products_and_variants_are_found()
    {
        await using Fixture f = await StartAsync();
        await InventoryEnv.AddProductAsync(f.Env, "i5-a", "Draftzzz Lamp", ["I5ZZ-ONE"], productActive: false);
        await InventoryEnv.AddProductAsync(f.Env, "i5-b", "Hidden Varzzz", ["I5ZZ-TWO"], variantActive: false);

        IdentityTestEnv.Api r = await f.SearchAsync("zzz");

        Assert.Equal(["Draftzzz Lamp", "Hidden Varzzz"], Names(r, "products", "name"));
        Assert.Equal(["I5ZZ-ONE", "I5ZZ-TWO"], Names(await f.SearchAsync("i5zz"), "skus", "sku"));
    }

    [Fact]
    public async Task I6_Support_never_gets_products_or_skus_but_still_gets_orders()
    {
        await using Fixture f = await StartAsync();
        Guid orderId = await f.OrderAsync("ORD-2026-000777");
        await InventoryEnv.AddProductAsync(f.Env, "i6-mug", "Blue Mug", ["MUG-BLU-01"]);
        await InventoryEnv.AddProductAsync(f.Env, "i6-stand", "ORD-2026-000777 Stand", ["ORD-2026-000777-S"]);

        IdentityTestEnv.Api mug = await f.SearchAsync("mug", f.Support);
        Assert.Equal(HttpStatusCode.OK, mug.Status);
        Assert.Empty(mug.Json.GetProperty("products").EnumerateArray());
        Assert.Empty(mug.Json.GetProperty("skus").EnumerateArray());

        IdentityTestEnv.Api number = await f.SearchAsync("000777", f.Support);
        Assert.Equal([orderId.ToString()], Names(number, "orders", "id"));
        Assert.Empty(number.Json.GetProperty("products").EnumerateArray());
        Assert.Empty(number.Json.GetProperty("skus").EnumerateArray());

        IdentityTestEnv.Api asAdmin = await f.SearchAsync("000777", f.Admin);
        Assert.Single(asAdmin.Json.GetProperty("products").EnumerateArray());
        Assert.Single(asAdmin.Json.GetProperty("skus").EnumerateArray());
    }

    [Fact]
    public async Task I7_At_most_5_per_group_in_the_documented_order()
    {
        await using Fixture f = await StartAsync();
        for (int i = 1; i <= 6; i++)
        {
            await f.OrderAsync($"ORD-I7-{i:00}", Day.AddHours(i));
            await InventoryEnv.AddProductAsync(f.Env, $"i7-{i}", $"I7 Item {7 - i}", [$"I7SKU-{7 - i}"]);
        }

        IdentityTestEnv.Api r = await f.SearchAsync("i7");

        Assert.Equal(["ORD-I7-06", "ORD-I7-05", "ORD-I7-04", "ORD-I7-03", "ORD-I7-02"], Names(r, "orders", "orderNumber"));
        Assert.Equal(["I7 Item 1", "I7 Item 2", "I7 Item 3", "I7 Item 4", "I7 Item 5"], Names(r, "products", "name"));
        Assert.Equal(["I7SKU-1", "I7SKU-2", "I7SKU-3", "I7SKU-4", "I7SKU-5"], Names(r, "skus", "sku"));
    }

    [Fact]
    public async Task I8_Response_has_exactly_the_contract_fields_and_no_money_or_email()
    {
        await using Fixture f = await StartAsync();
        await f.OrderAsync("ORD-I8-001");
        await InventoryEnv.AddProductAsync(f.Env, "i8", "I8 Thing", ["I8-SKU"]);

        IdentityTestEnv.Api r = await f.SearchAsync("i8");

        Assert.Equal(["orders", "products", "skus"], PropertyNames(r.Json));
        Assert.Equal(["id", "orderNumber", "status"], PropertyNames(Assert.Single(r.Json.GetProperty("orders").EnumerateArray())));
        Assert.Equal(["id", "name"], PropertyNames(Assert.Single(r.Json.GetProperty("products").EnumerateArray())));
        Assert.Equal(["productId", "productName", "sku"], PropertyNames(Assert.Single(r.Json.GetProperty("skus").EnumerateArray())));
        string raw = r.Json.GetRawText();
        foreach (string banned in new[] { "grandTotal", "amount", "price", "email", "customerEmail" })
        {
            Assert.DoesNotContain(banned, raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task I9_LIKE_wildcards_and_backslashes_are_literal()
    {
        await using Fixture f = await StartAsync();
        await InventoryEnv.AddProductAsync(f.Env, "i9-a", "50% Off Tee", ["AAB-1"]);
        await InventoryEnv.AddProductAsync(f.Env, "i9-b", "500 Club Tee", ["AXB-1"]);
        await InventoryEnv.AddProductAsync(f.Env, "i9-c", "a_b Tee", ["I9C-1"]);
        await InventoryEnv.AddProductAsync(f.Env, "i9-d", "aXb Tee", ["I9D-1"]);
        await f.OrderAsync("ORD-I9-000123");

        Assert.Equal(["50% Off Tee"], Names(await f.SearchAsync("0%25"), "products", "name"));
        IdentityTestEnv.Api underscore = await f.SearchAsync("a_b");
        Assert.Equal(["a_b Tee"], Names(underscore, "products", "name"));
        Assert.Empty(underscore.Json.GetProperty("skus").EnumerateArray());
        Assert.Empty((await f.SearchAsync("0%25")).Json.GetProperty("orders").EnumerateArray());
        Assert.Empty((await f.SearchAsync("0_")).Json.GetProperty("orders").EnumerateArray());
        IdentityTestEnv.Api backslash = await f.SearchAsync("%5Cz");
        Assert.Equal(HttpStatusCode.OK, backslash.Status);
        Assert.Empty(backslash.Json.GetProperty("products").EnumerateArray());
        Assert.Empty(backslash.Json.GetProperty("skus").EnumerateArray());
        Assert.Empty(backslash.Json.GetProperty("orders").EnumerateArray());
    }

    [Fact]
    public async Task I10_Matching_ignores_case()
    {
        await using Fixture f = await StartAsync();
        await f.OrderAsync("ORD-2026-000999");
        await InventoryEnv.AddProductAsync(f.Env, "i10", "Blue Mug", ["MUG-I10"]);

        Assert.Equal(["ORD-2026-000999"], Names(await f.SearchAsync("ord-2026"), "orders", "orderNumber"));
        Assert.Equal(["Blue Mug"], Names(await f.SearchAsync("MUG"), "products", "name"));
    }
}
