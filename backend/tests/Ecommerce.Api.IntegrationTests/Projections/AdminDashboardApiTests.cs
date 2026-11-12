using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Inventory;
using Npgsql;

namespace Ecommerce.Api.IntegrationTests.Projections;

// Endpoints of Slice 6b: happy path, 401, 403, 400 and the 404 and 409 answers (arch 9; Tests 7 and 18).
public class AdminDashboardApiTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- GET /admin/dashboard ----

    [Fact]
    public async Task Dashboard_without_a_token_is_401_and_as_a_Customer_is_403()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);

        Assert.Equal(HttpStatusCode.Unauthorized, (await e.DashboardAsync(null)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.DashboardAsync(e.R.TokenA)).Status);
    }

    [Fact]
    public async Task Dashboard_for_an_Admin_has_the_servers_KPIs_and_for_Support_has_kpis_null()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        Guid order = await e.PaidOrderAsync("DASH-ROLE", 2);
        decimal total = await e.TotalOfAsync(order);
        await e.DrainAsync();

        IdentityTestEnv.Api admin = await e.DashboardAsync(e.R.Admin, "today");
        IdentityTestEnv.Api support = await e.DashboardAsync(e.R.Support, "today");

        Assert.Equal(HttpStatusCode.OK, admin.Status);
        JsonElement kpis = admin.Json.GetProperty("kpis");
        Assert.Equal(ProjectionEnv.Money(total), kpis.GetProperty("revenue").GetProperty("amount").GetString());
        Assert.Equal("USD", kpis.GetProperty("revenue").GetProperty("currency").GetString());
        Assert.Equal(1, kpis.GetProperty("orderCount").GetInt32());
        Assert.Equal((1, 1, 0), (
            kpis.GetProperty("customers").GetProperty("total").GetInt32(),
            kpis.GetProperty("customers").GetProperty("new").GetInt32(),
            kpis.GetProperty("customers").GetProperty("returning").GetInt32()));
        Assert.Equal(HttpStatusCode.OK, support.Status);
        Assert.Equal(JsonValueKind.Null, support.Json.GetProperty("kpis").ValueKind);
        Assert.DoesNotContain(ProjectionEnv.Money(total), support.Text);
        Assert.Equal("today", support.Json.GetProperty("period").GetString());
    }

    [Theory]
    [InlineData("yesterday")]
    [InlineData("1d")]
    [InlineData("7D")]
    public async Task Dashboard_with_an_unknown_period_is_400(string period)
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);

        IdentityTestEnv.Api r = await e.DashboardAsync(e.R.Admin, period);

        Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        Assert.True(r.Json.GetProperty("errors").TryGetProperty("period", out _));
    }

    [Theory]
    [InlineData("7d")]
    [InlineData("30d")]
    public async Task Dashboard_accepts_the_7d_and_30d_periods(string period)
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);

        Assert.Equal(period, (await e.OkDashboardAsync(period)).Json.GetProperty("period").GetString());
    }

    [Fact]
    public async Task Low_stock_lists_a_variant_at_the_threshold_and_not_one_above_it()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        Guid atThreshold = await e.F.Cart.VariantAsync("LOW-3", stock: 3);
        Guid above = await e.F.Cart.VariantAsync("LOW-4", stock: 4);

        IdentityTestEnv.Api dashboard = await e.OkDashboardAsync();

        List<string> skus = dashboard.Json.GetProperty("lowStock").EnumerateArray().Select(l => l.GetProperty("sku").GetString()!).ToList();
        Assert.Contains("LOW-3", skus);
        Assert.DoesNotContain("LOW-4", skus);
        JsonElement row = dashboard.Json.GetProperty("lowStock").EnumerateArray().Single(l => l.GetProperty("sku").GetString() == "LOW-3");
        Assert.Equal((atThreshold, 3), (row.GetProperty("variantId").GetGuid(), row.GetProperty("available").GetInt32()));
        Assert.NotEqual(above, row.GetProperty("variantId").GetGuid());
    }

    [Fact]
    public async Task An_automatic_refund_leaves_revenue_unchanged_because_it_returns_money_that_was_never_revenue()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        Guid paid = await e.PaidOrderAsync("DASH-AUTO-A");
        decimal total = await e.TotalOfAsync(paid);
        Guid variant = await e.F.Cart.VariantAsync("DASH-AUTO-B", stock: 10);
        Guid expired = await e.F.PlaceOrderAsync(variant, 1, "auto-" + Guid.NewGuid().ToString("N"));
        string intent = await e.F.IntentOfAsync(expired);
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddMinutes(16);
        Assert.Equal(1, await e.F.SweepAsync());
        Assert.Equal(HttpStatusCode.OK, (await e.F.WebhookAsync("evt_dash_late", intent)).Status);
        await e.R.RefundWebhookAsync(await e.F.ScalarAsync<Guid>("SELECT id FROM refunds WHERE actor_id IS NULL"), "succeeded");
        await e.DrainAsync();

        IdentityTestEnv.Api dashboard = await e.Env.GetAsync("/api/v1/admin/dashboard", await e.Env.AccessTokenAsync("admin@example.test")); // the clock moved past the old token's life

        Assert.Equal(total, ProjectionEnv.AmountOf(dashboard.Json.GetProperty("kpis").GetProperty("revenue")));
        Assert.Equal(1, dashboard.Json.GetProperty("kpis").GetProperty("orderCount").GetInt32());
    }

    // ---- GET /admin/dashboard/recent-orders ----

    [Fact]
    public async Task Recent_orders_without_a_token_is_401_and_as_a_Customer_is_403_and_Support_may_read()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        const string url = "/api/v1/admin/dashboard/recent-orders";

        Assert.Equal(HttpStatusCode.Unauthorized, (await e.Env.GetAsync(url)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.GetAsync(url, e.R.TokenA)).Status);
        Assert.Equal(HttpStatusCode.OK, (await e.Env.GetAsync(url, e.R.Support)).Status);
    }

    [Fact]
    public async Task Recent_orders_are_newest_first_with_the_customer_email_and_include_a_PendingPayment_order()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        Guid older = await e.PaidOrderAsync("RECENT-A");
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddMinutes(1);
        Guid variant = await e.F.Cart.VariantAsync("RECENT-B", stock: 5);
        Guid pending = await e.F.PlaceOrderAsync(variant, 1, "recent-" + Guid.NewGuid().ToString("N"));

        IdentityTestEnv.Api r = await e.Env.GetAsync("/api/v1/admin/dashboard/recent-orders?page=1&pageSize=10", e.R.Admin);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        JsonElement[] items = [.. r.Json.GetProperty("items").EnumerateArray()];
        Assert.Equal(2, r.Json.GetProperty("totalCount").GetInt32());
        Assert.Equal(pending, items[0].GetProperty("id").GetGuid());
        Assert.Equal("PendingPayment", items[0].GetProperty("status").GetString());
        Assert.Equal(older, items[1].GetProperty("id").GetGuid());
        Assert.Equal("a@example.test", items[1].GetProperty("customerEmail").GetString());
        Assert.Equal(ProjectionEnv.Money(await e.TotalOfAsync(older)), items[1].GetProperty("grandTotal").GetProperty("amount").GetString());
    }

    [Theory]
    [InlineData("page=0&pageSize=10", "page")]
    [InlineData("page=1&pageSize=0", "pageSize")]
    [InlineData("page=1&pageSize=51", "pageSize")]
    public async Task Recent_orders_with_a_bad_page_or_page_size_is_400(string query, string field)
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);

        IdentityTestEnv.Api r = await e.Env.GetAsync("/api/v1/admin/dashboard/recent-orders?" + query, e.R.Admin);

        Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        Assert.True(r.Json.GetProperty("errors").TryGetProperty(field, out _));
    }

    // ---- POST /admin/projections/{name}/replay ----

    [Fact]
    public async Task Replay_is_401_anonymous_and_403_for_a_Customer_and_for_Support()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        const string url = "/api/v1/admin/projections/admin.dashboard/replay";

        Assert.Equal(HttpStatusCode.Unauthorized, (await e.Env.PostAsync(url)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.PostAsync(url, null, e.R.TokenA)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.PostAsync(url, null, e.R.Support)).Status);
    }

    [Fact]
    public async Task Replay_of_an_unknown_projector_is_404_and_of_a_known_one_is_202()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);

        Assert.Equal(HttpStatusCode.NotFound, (await e.Env.PostAsync("/api/v1/admin/projections/nope/replay", null, e.R.Admin)).Status);
        Assert.Equal(HttpStatusCode.Accepted, (await e.Env.PostAsync("/api/v1/admin/projections/admin.dashboard/replay", null, e.R.Admin)).Status);
    }

    [Fact]
    public async Task Replay_while_the_projector_is_held_by_another_worker_is_409_and_changes_nothing()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        await e.PaidOrderAsync("REPLAY-BUSY");
        await e.DrainAsync();
        await using NpgsqlConnection holder = new(ProjectionTestSupport.ConnectionString(e.Env));
        await holder.OpenAsync(Ct);
        await using (NpgsqlCommand cmd = new("SELECT pg_advisory_lock(hashtext('projection:admin.dashboard'))", holder))
        {
            await cmd.ExecuteScalarAsync(Ct);
        }

        IdentityTestEnv.Api r = await e.Env.PostAsync("/api/v1/admin/projections/admin.dashboard/replay", null, e.R.Admin);

        Assert.Equal(HttpStatusCode.Conflict, r.Status);
        Assert.Equal(1, await e.F.CountAsync("SELECT count(*) FROM admin_dashboard_orders"));
    }

    // ---- Test 14 end to end: a version that never arrives dead-letters and the dashboard says so ----

    [Fact]
    public async Task T14_a_missing_order_version_dead_letters_the_later_event_applies_nothing_past_it_and_the_dashboard_reports_it()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        Guid order = Guid.NewGuid();
        Guid user = Guid.NewGuid();
        foreach ((int version, string to) in new[] { (1, "Processing"), (3, "Delivered") })
        {
            string payload = JsonSerializer.Serialize(new { orderId = order, userId = user, fromStatus = "Paid", toStatus = to, occurredAt = e.Env.Clock.UtcNow });
            await ProjectionTestSupport.AddEventAsync(e.Env, "OrderStatusChanged", order, version, "Order", e.Env.Clock.UtcNow, payload);
        }

        for (int i = 0; i < 6; i++)
        {
            await e.DrainAsync();
            e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddMinutes(10);
        }

        Assert.Equal("Processing", await e.F.ScalarAsync<string>("SELECT status FROM admin_dashboard_orders WHERE order_id = @o", ("o", order)));
        IdentityTestEnv.Api dashboard = await e.Env.GetAsync("/api/v1/admin/dashboard", await e.Env.AccessTokenAsync("admin@example.test"));
        Assert.Equal(HttpStatusCode.OK, dashboard.Status);
        Assert.Contains(
            dashboard.Json.GetProperty("needsAttention").GetProperty("deadLetters").EnumerateArray(),
            d => d.GetProperty("consumer").GetString() == "admin.dashboard" && d.GetProperty("count").GetInt32() == 1);
    }

    // ---- Test 18: a stopped projector never blocks the order itself ----

    [Fact]
    public async Task T18_with_the_projector_stopped_the_order_is_Paid_and_correct_while_the_dashboard_reports_its_age_and_the_lag()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        Guid order = await e.PaidOrderAsync("DASH-18");
        DateTimeOffset paidAt = await e.F.ScalarAsync<DateTimeOffset>("SELECT occurred_at FROM outbox_events WHERE event_type = 'OrderPaid'");
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddMinutes(5);

        IdentityTestEnv.Api detail = await e.F.OrderAsync(e.R.TokenA, order);
        IdentityTestEnv.Api dashboard = await e.OkDashboardAsync();

        Assert.Equal("Paid", detail.Json.GetProperty("status").GetString());
        Assert.Equal(paidAt, dashboard.Json.GetProperty("asOf").GetDateTimeOffset());
        JsonElement lagging = dashboard.Json.GetProperty("needsAttention").GetProperty("laggingProjectors");
        Assert.Contains(lagging.EnumerateArray(), p => p.GetProperty("name").GetString() == "admin.dashboard" && p.GetProperty("lagSeconds").GetInt32() >= 300);
        Assert.Equal(0, dashboard.Json.GetProperty("kpis").GetProperty("orderCount").GetInt32());
    }
}
