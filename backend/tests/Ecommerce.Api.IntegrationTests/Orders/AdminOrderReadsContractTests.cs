using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Refunds;
using Ecommerce.Identity.Domain;

namespace Ecommerce.Api.IntegrationTests.Orders;

// P2.73 pins: the MVC contract of GET admin/orders and admin/orders/{id}, written and green before the Minimal API conversion.
public class AdminOrderReadsContractTests(PostgresFixture postgres)
{
    private const string Url = "/api/v1/admin/orders";
    private static readonly DateTimeOffset Day = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed record Seeded(IdentityTestEnv Env, string Cs, string Admin, Guid OrderA, Guid OrderB) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Env.DisposeAsync();
    }

    // Customer A has a Paid order, customer B a Shipped one.
    private async Task<Seeded> StartAsync()
    {
        IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        await env.AddUserAsync("admin@example.test", UserRole.Admin);
        User a = await env.AddUserAsync("a@example.test");
        User b = await env.AddUserAsync("b@example.test");
        string cs = OrdersEnv.ConnectionString(env);
        Guid orderA = await OrderSeed.InsertAsync(cs, a.Id, "Paid", Day);
        Guid orderB = await OrderSeed.InsertAsync(cs, b.Id, "Shipped", Day);
        return new Seeded(env, cs, await env.AccessTokenAsync("admin@example.test"), orderA, orderB);
    }

    private static List<Guid> Ids(IdentityTestEnv.Api r) =>
        r.Json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

    private static void AssertProblem(IdentityTestEnv.Api r)
    {
        Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        Assert.Equal("application/problem+json", r.Response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("?page=abc", "Page", "The value 'abc' is not valid.")]
    [InlineData("?pageSize=1.5", "PageSize", "The value '1.5' is not valid.")]
    [InlineData("?from=2026-13-01", "From", "The value '2026-13-01' is not valid.")]
    [InlineData("?to=x", "To", "The value 'x' is not valid.")]
    public async Task P1_a_bad_number_or_date_is_a_400_with_one_PascalCase_binding_error(string query, string key, string message)
    {
        await using Seeded s = await StartAsync();

        IdentityTestEnv.Api r = await s.Env.GetAsync(Url + query, s.Admin);

        AssertProblem(r);
        JsonProperty error = Assert.Single(r.Json.GetProperty("errors").EnumerateObject());
        Assert.Equal(key, error.Name);
        Assert.Equal(message, Assert.Single(error.Value.EnumerateArray()).GetString());
    }

    [Fact]
    public async Task P2_a_binding_error_is_returned_alone_without_the_validator_errors()
    {
        await using Seeded s = await StartAsync();

        IdentityTestEnv.Api r = await s.Env.GetAsync(Url + "?page=abc&pageSize=0&status=paid", s.Admin);

        AssertProblem(r);
        Assert.Equal(["Page"], r.Json.GetProperty("errors").EnumerateObject().Select(p => p.Name));
    }

    [Theory]
    [InlineData("?status=&customer=&orderNumber=&page=&pageSize=&from=&to=")]
    [InlineData("?status=%20&customer=%20%20")]
    public async Task P3_blank_query_values_are_ignored(string query)
    {
        await using Seeded s = await StartAsync();

        IdentityTestEnv.Api r = await s.Env.GetAsync(Url + query, s.Admin);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        Assert.Equal(new[] { s.OrderA, s.OrderB }.Order(), Ids(r).Order());
        Assert.Equal(1, r.Json.GetProperty("page").GetInt32());
        Assert.Equal(20, r.Json.GetProperty("pageSize").GetInt32());
    }

    [Theory]
    [InlineData("?status=Paid&status=Shipped", "A")]
    [InlineData("?customer=a%40example.test&customer=b%40example.test", "A")]
    [InlineData("?page=1&page=abc", "Both")]
    public async Task P4_a_repeated_key_uses_its_first_value(string query, string expected)
    {
        await using Seeded s = await StartAsync();

        IdentityTestEnv.Api r = await s.Env.GetAsync(Url + query, s.Admin);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        Guid[] want = expected == "A" ? [s.OrderA] : new[] { s.OrderA, s.OrderB }.Order().ToArray();
        Assert.Equal(want, Ids(r).Order());
    }

    [Fact]
    public async Task P5_validator_errors_are_camelCase_with_the_problem_fields()
    {
        await using Seeded s = await StartAsync();

        IdentityTestEnv.Api r = await s.Env.GetAsync(Url + "?status=paid&pageSize=0", s.Admin);

        AssertProblem(r);
        Assert.True(r.Json.TryGetProperty("type", out _));
        Assert.True(r.Json.TryGetProperty("title", out _));
        Assert.True(r.Json.TryGetProperty("traceId", out _));
        Assert.True(r.Json.TryGetProperty("correlationId", out _));
        JsonElement errors = r.Json.GetProperty("errors");
        Assert.Equal("Choose a valid order status.", errors.GetProperty("status")[0].GetString());
        Assert.Equal("Page size must be 1 to 100.", errors.GetProperty("pageSize")[0].GetString());
    }

    [Theory]
    [InlineData("?pageSize=100")]
    [InlineData("?from=2026-06-10&to=2026-06-10")]
    public async Task P6_the_upper_page_size_and_a_same_day_range_are_accepted(string query)
    {
        await using Seeded s = await StartAsync();

        Assert.Equal(HttpStatusCode.OK, (await s.Env.GetAsync(Url + query, s.Admin)).Status);
    }

    [Fact]
    public async Task P7_an_unknown_order_is_a_404_problem()
    {
        await using Seeded s = await StartAsync();

        IdentityTestEnv.Api r = await s.Env.GetAsync($"{Url}/{Guid.NewGuid()}", s.Admin);

        Assert.Equal(HttpStatusCode.NotFound, r.Status);
        Assert.Equal("application/problem+json", r.Response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Order not found", r.Json.GetProperty("title").GetString());
    }

    [Fact]
    public async Task P8_support_reads_exactly_what_admin_reads_for_an_order_with_a_refund()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        Assert.Equal(HttpStatusCode.Accepted, (await e.RefundAsync(order, "10.00")).Status);

        foreach (string url in new[] { Url, $"{Url}/{order}" })
        {
            IdentityTestEnv.Api admin = await e.Env.GetAsync(url, e.Admin);
            IdentityTestEnv.Api support = await e.Env.GetAsync(url, e.Support);

            Assert.Equal(HttpStatusCode.OK, admin.Status);
            Assert.Equal(admin.Text, support.Text);
        }
    }

    [Fact]
    public async Task P9_the_admin_detail_embeds_the_same_order_as_the_customer_detail()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        DateTimeOffset t0 = e.Env.Clock.UtcNow;
        Guid order = await OrderSeed.InsertAsync(
            e.Cs, e.CustomerId, "Cancelled", t0,
            history:
            [
                new SeedHistory(null, "PendingPayment", t0),
                new SeedHistory("PendingPayment", "Cancelled", t0.AddMinutes(16), "reservation-expired"),
            ]);
        await e.F.Cart.ExecAsync(
            "INSERT INTO payments (id, order_id, attempt, provider, provider_payment_id, amount, currency, status, created_at, updated_at) " +
            "VALUES (@id, @o, 1, 'stripe', @pi, @amount, 'USD', 'Succeeded', @at, @at)",
            ("id", Guid.NewGuid()), ("o", order), ("pi", "pi_seed_" + Guid.NewGuid().ToString("N")), ("amount", RefundEnv.Captured), ("at", t0));

        IdentityTestEnv.Api admin = await e.Env.GetAsync($"{Url}/{order}", e.Admin);
        IdentityTestEnv.Api customer = await e.Env.GetAsync($"/api/v1/orders/{order}", e.TokenA);

        Assert.Equal(HttpStatusCode.OK, admin.Status);
        JsonElement embedded = admin.Json.GetProperty("order");
        Assert.Equal(customer.Json.GetRawText(), embedded.GetRawText());
        Assert.NotEqual(JsonValueKind.Null, embedded.GetProperty("statusNote").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, embedded.GetProperty("payment").ValueKind);
        Assert.All(embedded.GetProperty("timeline").EnumerateArray(), t => Assert.False(string.IsNullOrEmpty(t.GetProperty("description").GetString())));
    }
}
