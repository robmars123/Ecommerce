using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Orders;
using Ecommerce.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Api.IntegrationTests.Admin;

// Slice 12.1: GET /api/v1/admin/customers and /{id} (app-context 12.1, arch 13.2 "Admin customers API").
public class AdminCustomersApiTests(PostgresFixture postgres)
{
    private const string Url = "/api/v1/admin/customers";
    private static readonly DateTimeOffset Day = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed class Fixture(IdentityTestEnv env, string cs, string admin, string support, Guid adminId) : IAsyncDisposable
    {
        public IdentityTestEnv Env { get; } = env;
        public string Cs { get; } = cs;
        public string Admin { get; } = admin;
        public string Support { get; } = support;
        public Guid AdminId { get; } = adminId;

        public Task<IdentityTestEnv.Api> ListAsync(string query = "") => Env.GetAsync(Url + query, Admin);

        public Task<IdentityTestEnv.Api> DetailAsync(Guid id, string? token = null) => Env.GetAsync($"{Url}/{id}", token ?? Admin);

        public async Task<User> CustomerAsync(string email, string? displayName = null, DateTimeOffset? createdAt = null)
        {
            User user = await Env.AddUserAsync(email);
            if (displayName is not null)
            {
                await Env.Db.Database.ExecuteSqlAsync($"UPDATE users SET display_name = {displayName} WHERE id = {user.Id}", TestContext.Current.CancellationToken);
            }
            if (createdAt is DateTimeOffset at)
            {
                await Env.Db.Database.ExecuteSqlAsync($"UPDATE users SET created_at = {at} WHERE id = {user.Id}", TestContext.Current.CancellationToken);
            }
            return user;
        }

        /// <summary>An order whose grand total is exactly <paramref name="grand"/> (5.00 shipping is part of it). Ever-paid orders get the Paid history row.</summary>
        public Task<Guid> OrderAsync(Guid userId, string status, decimal grand, DateTimeOffset at, bool everPaid, string? number = null) =>
            OrderSeed.InsertAsync(
                Cs, userId, status, at,
                [new SeedItem("TEE", "Tee", grand - OrderSeed.ShippingTotal, 1)],
                everPaid
                    ? [new SeedHistory(null, "PendingPayment", at), new SeedHistory("PendingPayment", "Paid", at.AddMinutes(1)), new SeedHistory("Paid", status, at.AddMinutes(2))]
                    : [new SeedHistory(null, "PendingPayment", at), new SeedHistory("PendingPayment", status, at.AddMinutes(1))],
                null, number);

        public async Task<Guid> PaymentAsync(Guid orderId, decimal amount)
        {
            Guid id = Guid.NewGuid();
            string providerId = "pi_seed_" + id.ToString("N");
            await Env.Db.Database.ExecuteSqlAsync(
                $"INSERT INTO payments (id, order_id, attempt, provider, provider_payment_id, amount, currency, status, created_at, updated_at) VALUES ({id}, {orderId}, 1, 'stripe', {providerId}, {amount}, 'USD', 'Succeeded', {Day}, {Day})",
                TestContext.Current.CancellationToken);
            return id;
        }

        public async Task<Guid> RefundAsync(Guid paymentId, decimal amount, string status, bool automatic, DateTimeOffset? at = null)
        {
            Guid id = Guid.NewGuid();
            string key = "seed-" + id.ToString("N");
            DateTimeOffset created = at ?? Day;
            if (automatic)
            {
                await Env.Db.Database.ExecuteSqlAsync(
                    $"INSERT INTO refunds (id, payment_id, amount, currency, status, reason, actor_id, idempotency_key, created_at) VALUES ({id}, {paymentId}, {amount}, 'USD', {status}, 'duplicate-capture', NULL, {key}, {created})",
                    TestContext.Current.CancellationToken);
            }
            else
            {
                await Env.Db.Database.ExecuteSqlAsync(
                    $"INSERT INTO refunds (id, payment_id, amount, currency, status, reason, actor_id, idempotency_key, created_at) VALUES ({id}, {paymentId}, {amount}, 'USD', {status}, 'Damaged', {AdminId}, {key}, {created})",
                    TestContext.Current.CancellationToken);
            }
            return id;
        }

        public async Task<Guid> ReturnAsync(Guid orderId, string status, DateTimeOffset at)
        {
            Guid id = Guid.NewGuid();
            await Env.Db.Database.ExecuteSqlAsync(
                $"INSERT INTO return_requests (id, order_id, items, reason, status, created_at) VALUES ({id}, {orderId}, CAST('[]' AS jsonb), 'Too small', {status}, {at})",
                TestContext.Current.CancellationToken);
            return id;
        }

        public ValueTask DisposeAsync() => Env.DisposeAsync();
    }

    private async Task<Fixture> StartAsync()
    {
        IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User admin = await env.AddUserAsync("admin@example.test", UserRole.Admin);
        await env.AddUserAsync("support@example.test", UserRole.Support);
        return new Fixture(env, OrdersEnv.ConnectionString(env), await env.AccessTokenAsync("admin@example.test"), await env.AccessTokenAsync("support@example.test"), admin.Id);
    }

    private static List<string> Emails(IdentityTestEnv.Api r) =>
        r.Json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("email").GetString()!).ToList();

    [Fact]
    public async Task I1_anonymous_is_401_customer_is_403_admin_and_support_are_200_on_both_endpoints()
    {
        await using Fixture f = await StartAsync();
        User customer = await f.CustomerAsync("customer@example.test");
        string customerToken = await f.Env.AccessTokenAsync("customer@example.test");

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Env.GetAsync(Url)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Env.GetAsync($"{Url}/{customer.Id}")).Status);

        IdentityTestEnv.Api forbidden = await f.Env.GetAsync(Url, customerToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);
        Assert.DoesNotContain("customer@example.test", forbidden.Text);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.DetailAsync(customer.Id, customerToken)).Status);

        Assert.Equal(HttpStatusCode.OK, (await f.ListAsync()).Status);
        Assert.Equal(HttpStatusCode.OK, (await f.Env.GetAsync(Url, f.Support)).Status);
        Assert.Equal(HttpStatusCode.OK, (await f.DetailAsync(customer.Id)).Status);
        Assert.Equal(HttpStatusCode.OK, (await f.DetailAsync(customer.Id, f.Support)).Status);
    }

    [Fact]
    public async Task I2_support_gets_total_spent_null_admin_gets_the_amount_everything_else_equal()
    {
        await using Fixture f = await StartAsync();
        User customer = await f.CustomerAsync("buyer@example.test");
        Guid order = await f.OrderAsync(customer.Id, "Paid", 30.00m, Day, true, "ORD-2026-000001");
        await f.PaymentAsync(order, 30.00m);

        IdentityTestEnv.Api asAdmin = await f.DetailAsync(customer.Id);
        IdentityTestEnv.Api asSupport = await f.DetailAsync(customer.Id, f.Support);

        Assert.Equal("30.00", asAdmin.Json.GetProperty("totalSpent").GetProperty("amount").GetString());
        Assert.True(asSupport.Json.TryGetProperty("totalSpent", out JsonElement total));
        Assert.Equal(JsonValueKind.Null, total.ValueKind);
        Dictionary<string, string> adminRest = asAdmin.Json.EnumerateObject().Where(p => p.Name != "totalSpent").ToDictionary(p => p.Name, p => p.Value.GetRawText());
        Dictionary<string, string> supportRest = asSupport.Json.EnumerateObject().Where(p => p.Name != "totalSpent").ToDictionary(p => p.Name, p => p.Value.GetRawText());
        Assert.Equal(adminRest, supportRest);
        Assert.Equal(1, asSupport.Json.GetProperty("orderCount").GetInt32());
    }

    [Fact]
    public async Task I3_total_spent_is_paid_ever_minus_succeeded_admin_refunds_only()
    {
        await using Fixture f = await StartAsync();
        User customer = await f.CustomerAsync("buyer@example.test");
        User other = await f.CustomerAsync("other@example.test");
        Guid a = await f.OrderAsync(customer.Id, "Paid", 100.00m, Day, true);
        Guid b = await f.OrderAsync(customer.Id, "Paid", 40.00m, Day.AddHours(1), true);
        await f.OrderAsync(customer.Id, "PendingPayment", 50.00m, Day.AddHours(2), false);
        await f.OrderAsync(customer.Id, "Cancelled", 20.00m, Day.AddHours(3), false);
        Guid otherOrder = await f.OrderAsync(other.Id, "Paid", 999.00m, Day, true);
        Guid payA = await f.PaymentAsync(a, 100.00m);
        Guid payB = await f.PaymentAsync(b, 40.00m);
        Guid payOther = await f.PaymentAsync(otherOrder, 999.00m);
        await f.RefundAsync(payB, 30.00m, "Succeeded", false);
        await f.RefundAsync(payA, 5.00m, "Pending", false);
        await f.RefundAsync(payA, 7.00m, "Failed", false);
        await f.RefundAsync(payA, 100.00m, "Succeeded", true);
        await f.RefundAsync(payOther, 500.00m, "Succeeded", false);

        IdentityTestEnv.Api r = await f.DetailAsync(customer.Id);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        Assert.Equal("110.00", r.Json.GetProperty("totalSpent").GetProperty("amount").GetString());
        Assert.Equal("USD", r.Json.GetProperty("totalSpent").GetProperty("currency").GetString());
    }

    [Fact]
    public async Task I4_lists_only_customers_newest_first_with_the_id_tie_breaker_and_pages()
    {
        await using Fixture f = await StartAsync();
        User oldest = await f.CustomerAsync("oldest@example.test", null, Day.AddDays(-2));
        User tieOne = await f.CustomerAsync("tie1@example.test", null, Day);
        User tieTwo = await f.CustomerAsync("tie2@example.test", null, Day);

        IdentityTestEnv.Api r = await f.ListAsync();

        Assert.Equal(HttpStatusCode.OK, r.Status);
        List<string> ties = new List<User> { tieOne, tieTwo }.OrderByDescending(u => u.Id).Select(u => u.Email).ToList();
        Assert.Equal([ties[0], ties[1], oldest.Email], Emails(r));
        Assert.DoesNotContain("admin@example.test", r.Text);
        Assert.DoesNotContain("support@example.test", r.Text);
        Assert.Equal(3, r.Json.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, r.Json.GetProperty("totalPages").GetInt32());
        Assert.Equal(20, r.Json.GetProperty("pageSize").GetInt32());

        IdentityTestEnv.Api paged = await f.ListAsync("?page=2&pageSize=2");
        Assert.Equal([oldest.Email], Emails(paged));
        Assert.Equal(2, paged.Json.GetProperty("totalPages").GetInt32());

        IdentityTestEnv.Api past = await f.ListAsync("?page=9&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, past.Status);
        Assert.Empty(Emails(past));
        Assert.Equal(3, past.Json.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task I5_search_matches_email_or_name_substrings_ignoring_case_and_treats_wildcards_literally()
    {
        await using Fixture f = await StartAsync();
        await f.CustomerAsync("a_b@example.test", "Ann");
        await f.CustomerAsync("axb@example.test", "Bob");
        await f.CustomerAsync("50%off@example.test", "Cy");
        await f.CustomerAsync("zed@example.test", "Zelda Fitz");

        Assert.Equal(["zed@example.test"], Emails(await f.ListAsync("?search=ZELDA")));
        Assert.Equal(["zed@example.test"], Emails(await f.ListAsync("?search=elda%20f")));
        Assert.Equal(["a_b@example.test"], Emails(await f.ListAsync("?search=A_B@EXAMPLE")));
        Assert.Equal(["a_b@example.test"], Emails(await f.ListAsync("?search=_")));
        Assert.Equal(["50%off@example.test"], Emails(await f.ListAsync("?search=%25")));
        Assert.Equal(4, (await f.ListAsync("?search=%20%20")).Json.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await f.ListAsync("?search=" + new string('a', 100))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.ListAsync("?search=" + new string('a', 101))).Status);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task I6_out_of_range_paging_is_400(string query)
    {
        await using Fixture f = await StartAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await f.ListAsync(query)).Status);
    }

    [Fact]
    public async Task I7_detail_of_an_unknown_staff_or_malformed_id_is_404()
    {
        await using Fixture f = await StartAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await f.DetailAsync(Guid.NewGuid())).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await f.DetailAsync(f.AdminId)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Env.GetAsync(Url + "/not-a-guid", f.Admin)).Status);
    }

    [Fact]
    public async Task I8_detail_has_counts_and_the_latest_ten_of_each_list_and_zero_for_a_customer_with_no_orders()
    {
        await using Fixture f = await StartAsync();
        User customer = await f.CustomerAsync("buyer@example.test", "Buyer Bee", Day);
        User empty = await f.CustomerAsync("empty@example.test");
        List<Guid> orders = [];
        for (int i = 0; i < 12; i++)
        {
            Guid order = await f.OrderAsync(customer.Id, "Paid", 25.00m, Day.AddHours(i), true, $"ORD-2026-{i + 100:D6}");
            orders.Add(order);
            Guid payment = await f.PaymentAsync(order, 25.00m);
            await f.RefundAsync(payment, 1.00m, "Succeeded", i == 11, Day.AddHours(i));
            await f.ReturnAsync(order, "Requested", Day.AddHours(i));
        }

        IdentityTestEnv.Api r = await f.DetailAsync(customer.Id);

        Assert.Equal("Buyer Bee", r.Str("displayName"));
        Assert.Equal(12, r.Json.GetProperty("orderCount").GetInt32());
        JsonElement recent = r.Json.GetProperty("recentOrders");
        Assert.Equal(10, recent.GetArrayLength());
        Assert.Equal(orders[11], recent[0].GetProperty("id").GetGuid());
        Assert.Equal("ORD-2026-000111", recent[0].GetProperty("orderNumber").GetString());
        Assert.Equal("25.00", recent[0].GetProperty("grandTotal").GetProperty("amount").GetString());
        Assert.Equal(12, r.Json.GetProperty("returnCount").GetInt32());
        JsonElement returns = r.Json.GetProperty("recentReturns");
        Assert.Equal(10, returns.GetArrayLength());
        Assert.Equal("ORD-2026-000111", returns[0].GetProperty("orderNumber").GetString());
        Assert.Equal("Requested", returns[0].GetProperty("status").GetString());
        Assert.Equal(12, r.Json.GetProperty("refundCount").GetInt32());
        JsonElement refunds = r.Json.GetProperty("recentRefunds");
        Assert.Equal(10, refunds.GetArrayLength());
        Assert.Equal("ORD-2026-000111", refunds[0].GetProperty("orderNumber").GetString());
        Assert.True(refunds[0].GetProperty("automatic").GetBoolean());
        Assert.False(refunds[1].GetProperty("automatic").GetBoolean());
        Assert.DoesNotContain("actorId", r.Text);

        IdentityTestEnv.Api none = await f.DetailAsync(empty.Id);
        Assert.Equal(0, none.Json.GetProperty("orderCount").GetInt32());
        Assert.Equal(0, none.Json.GetProperty("recentOrders").GetArrayLength());
        Assert.Equal(0, none.Json.GetProperty("refundCount").GetInt32());
        Assert.Equal(0, none.Json.GetProperty("returnCount").GetInt32());
        Assert.Equal("0.00", none.Json.GetProperty("totalSpent").GetProperty("amount").GetString());
    }
}
