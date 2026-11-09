using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Identity.Domain;

namespace Ecommerce.Api.IntegrationTests.Orders;

// Slice 5 gap-fill 11.2: GET /api/v1/admin/orders (arch 13.2 "Admin order list and actions").
public class AdminOrderListApiTests(PostgresFixture postgres)
{
    private const string Url = "/api/v1/admin/orders";
    private static readonly DateTimeOffset Day = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed class Fixture(IdentityTestEnv env, string cs, string admin) : IAsyncDisposable
    {
        public IdentityTestEnv Env { get; } = env;
        public string Cs { get; } = cs;
        public string Admin { get; } = admin;

        public Task<IdentityTestEnv.Api> ListAsync(string query = "") => Env.GetAsync(Url + query, Admin);

        public ValueTask DisposeAsync() => Env.DisposeAsync();
    }

    private async Task<Fixture> StartAsync()
    {
        IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        await env.AddUserAsync("admin@example.test", UserRole.Admin);
        string token = await env.AccessTokenAsync("admin@example.test");
        return new Fixture(env, OrdersEnv.ConnectionString(env), token);
    }

    private static List<Guid> Ids(IdentityTestEnv.Api r) =>
        r.Json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

    [Fact]
    public async Task Anonymous_is_401_customer_is_403_without_data_and_support_and_admin_are_200()
    {
        await using Fixture f = await StartAsync();
        User customer = await f.Env.AddUserAsync("customer@example.test");
        await f.Env.AddUserAsync("support@example.test", UserRole.Support);
        string customerToken = await f.Env.AccessTokenAsync("customer@example.test");
        string supportToken = await f.Env.AccessTokenAsync("support@example.test");
        await OrderSeed.InsertAsync(f.Cs, customer.Id, "Paid", Day);

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Env.GetAsync(Url)).Status);

        IdentityTestEnv.Api forbidden = await f.Env.GetAsync(Url, customerToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);
        Assert.DoesNotContain("items", forbidden.Text);
        Assert.DoesNotContain("customer@example.test", forbidden.Text);

        Assert.Equal(HttpStatusCode.OK, (await f.Env.GetAsync(Url, supportToken)).Status);
        Assert.Equal(HttpStatusCode.OK, (await f.ListAsync()).Status);
    }

    [Fact]
    public async Task Lists_every_customers_orders_newest_first_with_the_id_tie_breaker_and_server_money()
    {
        await using Fixture f = await StartAsync();
        User a = await f.Env.AddUserAsync("a@example.test");
        User b = await f.Env.AddUserAsync("b@example.test");
        Guid oldest = await OrderSeed.InsertAsync(f.Cs, a.Id, "Paid", Day.AddDays(-2));
        Guid tieOne = await OrderSeed.InsertAsync(f.Cs, b.Id, "Shipped", Day);
        Guid tieTwo = await OrderSeed.InsertAsync(f.Cs, a.Id, "Paid", Day);

        IdentityTestEnv.Api r = await f.ListAsync();

        Assert.Equal(HttpStatusCode.OK, r.Status);
        List<Guid> expectedTies = new List<Guid> { tieOne, tieTwo }.OrderByDescending(g => g).ToList();
        Assert.Equal([expectedTies[0], expectedTies[1], oldest], Ids(r));
        Assert.Equal(3, r.Json.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, r.Json.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, r.Json.GetProperty("page").GetInt32());
        Assert.Equal(20, r.Json.GetProperty("pageSize").GetInt32());
        JsonElement last = r.Json.GetProperty("items")[2];
        Assert.Equal("a@example.test", last.GetProperty("customerEmail").GetString());
        Assert.Equal("Paid", last.GetProperty("status").GetString());
        Assert.Equal("25.00", last.GetProperty("grandTotal").GetProperty("amount").GetString());
        Assert.Equal("USD", last.GetProperty("grandTotal").GetProperty("currency").GetString());
    }

    [Fact]
    public async Task Status_filter_returns_only_that_status_and_a_bad_name_or_case_is_400()
    {
        await using Fixture f = await StartAsync();
        User a = await f.Env.AddUserAsync("a@example.test");
        Guid paid = await OrderSeed.InsertAsync(f.Cs, a.Id, "Paid", Day);
        await OrderSeed.InsertAsync(f.Cs, a.Id, "Shipped", Day);

        Assert.Equal([paid], Ids(await f.ListAsync("?status=Paid")));
        foreach (string bad in new[] { "paid", "Bogus", "3" })
        {
            IdentityTestEnv.Api r = await f.ListAsync($"?status={bad}");
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
            Assert.True(r.Json.GetProperty("errors").TryGetProperty("status", out _), r.Text);
        }
    }

    [Fact]
    public async Task Date_range_is_inclusive_in_utc_days_and_a_bad_range_is_400()
    {
        await using Fixture f = await StartAsync();
        User a = await f.Env.AddUserAsync("a@example.test");
        DateTimeOffset from = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
        Guid before = await OrderSeed.InsertAsync(f.Cs, a.Id, "Paid", from.AddSeconds(-1));
        Guid atFrom = await OrderSeed.InsertAsync(f.Cs, a.Id, "Paid", from);
        Guid lastSecond = await OrderSeed.InsertAsync(f.Cs, a.Id, "Paid", from.AddDays(1).AddSeconds(-1));
        Guid after = await OrderSeed.InsertAsync(f.Cs, a.Id, "Paid", from.AddDays(1));

        List<Guid> ids = Ids(await f.ListAsync("?from=2026-06-10&to=2026-06-10"));

        Assert.Contains(atFrom, ids);
        Assert.Contains(lastSecond, ids);
        Assert.DoesNotContain(before, ids);
        Assert.DoesNotContain(after, ids);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.ListAsync("?from=2026-06-11&to=2026-06-10")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.ListAsync("?from=2026-13-01")).Status);
    }

    [Fact]
    public async Task Customer_email_ignores_case_and_whitespace_and_an_unknown_email_is_an_empty_200()
    {
        await using Fixture f = await StartAsync();
        User a = await f.Env.AddUserAsync("a@example.test");
        User b = await f.Env.AddUserAsync("b@example.test");
        Guid orderA = await OrderSeed.InsertAsync(f.Cs, a.Id, "Paid", Day);
        await OrderSeed.InsertAsync(f.Cs, b.Id, "Paid", Day);

        Assert.Equal([orderA], Ids(await f.ListAsync("?customer=%20A%40Example.TEST%20")));

        IdentityTestEnv.Api none = await f.ListAsync("?customer=nobody%40example.test");
        Assert.Equal(HttpStatusCode.OK, none.Status);
        Assert.Empty(Ids(none));
        Assert.Equal(0, none.Json.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Order_number_is_an_exact_match_and_status_with_customer_is_an_and()
    {
        await using Fixture f = await StartAsync();
        User a = await f.Env.AddUserAsync("a@example.test");
        User b = await f.Env.AddUserAsync("b@example.test");
        Guid numbered = await OrderSeed.InsertAsync(f.Cs, a.Id, "Paid", Day, orderNumber: "ORD-2026-000042");
        Guid shippedA = await OrderSeed.InsertAsync(f.Cs, a.Id, "Shipped", Day);
        await OrderSeed.InsertAsync(f.Cs, b.Id, "Shipped", Day);

        Assert.Equal([numbered], Ids(await f.ListAsync("?orderNumber=ORD-2026-000042")));
        Assert.Empty(Ids(await f.ListAsync("?orderNumber=ORD-2026-999999")));
        Assert.Equal([shippedA], Ids(await f.ListAsync("?status=Shipped&customer=a%40example.test")));
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task Out_of_range_paging_is_400(string query)
    {
        await using Fixture f = await StartAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await f.ListAsync(query)).Status);
    }

    [Fact]
    public async Task A_page_past_the_end_is_an_empty_200_with_the_real_total()
    {
        await using Fixture f = await StartAsync();
        User a = await f.Env.AddUserAsync("a@example.test");
        await OrderSeed.InsertAsync(f.Cs, a.Id, "Paid", Day);
        await OrderSeed.InsertAsync(f.Cs, a.Id, "Paid", Day);

        IdentityTestEnv.Api r = await f.ListAsync("?page=3&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, r.Status);
        Assert.Empty(Ids(r));
        Assert.Equal(2, r.Json.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, r.Json.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Customer_email_is_null_when_the_orders_user_has_no_row()
    {
        await using Fixture f = await StartAsync();
        await OrderSeed.InsertAsync(f.Cs, Guid.NewGuid(), "Paid", Day);

        IdentityTestEnv.Api r = await f.ListAsync();

        Assert.Equal(HttpStatusCode.OK, r.Status);
        Assert.Equal(JsonValueKind.Null, r.Json.GetProperty("items")[0].GetProperty("customerEmail").ValueKind);
    }
}
