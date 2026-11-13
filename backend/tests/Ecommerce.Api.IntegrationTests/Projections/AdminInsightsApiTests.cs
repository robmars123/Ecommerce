using System.Globalization;
using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Refunds;

namespace Ecommerce.Api.IntegrationTests.Projections;

// P2-tier4: revenue trend, CSV export (Admin only) and read-only settings (Admin only). Happy path, 401, 403, 400.
// Settings takes no input, so it has no 400 test (plan D8).
public class AdminInsightsApiTests(PostgresFixture postgres)
{
    private const string Trend = "/api/v1/admin/dashboard/trend";
    private const string Export = "/api/v1/admin/dashboard/export";
    private const string Settings = "/api/v1/admin/settings";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateTimeOffset PinNoon(ProjectionEnv e)
    {
        DateTimeOffset now = e.Env.Clock.UtcNow;
        DateTimeOffset noon = new(now.UtcDateTime.Date.AddHours(12), TimeSpan.Zero);
        e.Env.Clock.UtcNow = noon > now ? noon : noon.AddDays(1);
        return e.Env.Clock.UtcNow;
    }

    private static async Task<HttpResponseMessage> RawGetAsync(ProjectionEnv e, string url, string token)
    {
        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await e.Env.Client.SendAsync(request, Ct);
    }

    // ---- I1, I2, I3: auth ----

    [Fact]
    public async Task Trend_without_a_token_is_401_as_a_Customer_is_403_and_as_Support_is_403_with_no_amount()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);

        Assert.Equal(HttpStatusCode.Unauthorized, (await e.Env.GetAsync(Trend)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.GetAsync(Trend, e.R.TokenA)).Status);
        IdentityTestEnv.Api support = await e.Env.GetAsync(Trend, e.R.Support);
        Assert.Equal(HttpStatusCode.Forbidden, support.Status);
        Assert.DoesNotContain("amount", support.Text);
    }

    [Fact]
    public async Task Export_without_a_token_is_401_as_a_Customer_is_403_and_as_Support_is_403()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);

        Assert.Equal(HttpStatusCode.Unauthorized, (await e.Env.GetAsync(Export)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.GetAsync(Export, e.R.TokenA)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.GetAsync(Export, e.R.Support)).Status);
    }

    [Fact]
    public async Task Settings_without_a_token_is_401_as_a_Customer_is_403_and_as_Support_is_403()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);

        Assert.Equal(HttpStatusCode.Unauthorized, (await e.Env.GetAsync(Settings)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.GetAsync(Settings, e.R.TokenA)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.GetAsync(Settings, e.R.Support)).Status);
    }

    // ---- I4, I5: 400 ----

    [Theory]
    [InlineData("yesterday")]
    [InlineData("1d")]
    [InlineData("7D")]
    public async Task Trend_and_export_with_an_unknown_period_are_400_with_the_period_key(string period)
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);

        foreach (string path in new[] { Trend, Export })
        {
            IdentityTestEnv.Api r = await e.Env.GetAsync(path + "?period=" + period, e.R.Admin);
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
            Assert.True(r.Json.GetProperty("errors").TryGetProperty("period", out _));
        }
    }

    // ---- I6, I7: trend ----

    [Fact]
    public async Task Trend_today_is_hourly_up_to_the_current_hour_and_matches_the_dashboard_revenue()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        DateTimeOffset t = PinNoon(e);
        string admin = await e.Env.AccessTokenAsync("admin@example.test");
        Guid order = await e.PaidOrderAsync("TREND-TODAY", 2, await e.Env.AccessTokenAsync("a@example.test"));
        decimal total = await e.TotalOfAsync(order);
        await e.DrainAsync();

        JsonElement trend = (await e.Env.GetAsync(Trend + "?period=today", admin)).Json;
        JsonElement kpis = (await e.Env.GetAsync("/api/v1/admin/dashboard?period=today", admin)).Json.GetProperty("kpis");

        Assert.Equal("hour", trend.GetProperty("granularity").GetString());
        JsonElement buckets = trend.GetProperty("buckets");
        Assert.Equal(t.UtcDateTime.Hour + 1, buckets.GetArrayLength());
        JsonElement hit = buckets.EnumerateArray().Single(b => b.GetProperty("orders").GetInt32() == 1);
        Assert.Equal(t.UtcDateTime.Date.AddHours(12), hit.GetProperty("start").GetDateTimeOffset().UtcDateTime);
        Assert.Equal(ProjectionEnv.Money(total), trend.GetProperty("total").GetProperty("amount").GetString());
        Assert.Equal(kpis.GetProperty("revenue").GetProperty("amount").GetString(), trend.GetProperty("total").GetProperty("amount").GetString());
        Assert.Equal(kpis.GetProperty("orderCount").GetInt32(), trend.GetProperty("orderCount").GetInt32());
    }

    [Fact]
    public async Task Trend_7d_sums_to_the_total_and_to_the_dashboard_revenue_after_an_actor_refund_and_30d_has_30_buckets()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        PinNoon(e);
        string admin = await e.Env.AccessTokenAsync("admin@example.test");
        Guid order = await e.PaidOrderAsync("TREND-7D", token: await e.Env.AccessTokenAsync("a@example.test"));
        decimal total = await e.TotalOfAsync(order);
        await e.R.RefundWebhookAsync(RefundEnv.RefundIdOf(await e.R.RefundAsync(order, "3.00", token: admin)), "succeeded");
        await e.DrainAsync();

        JsonElement trend = (await e.Env.GetAsync(Trend + "?period=7d", admin)).Json;
        JsonElement kpis = (await e.Env.GetAsync("/api/v1/admin/dashboard?period=7d", admin)).Json.GetProperty("kpis");

        decimal sum = trend.GetProperty("buckets").EnumerateArray().Sum(b => ProjectionEnv.AmountOf(b.GetProperty("revenue")));
        Assert.Equal(7, trend.GetProperty("buckets").GetArrayLength());
        Assert.Equal(total - 3.00m, sum);
        Assert.Equal(sum, ProjectionEnv.AmountOf(trend.GetProperty("total")));
        Assert.Equal(ProjectionEnv.AmountOf(kpis.GetProperty("revenue")), sum);
        Assert.Equal(30, (await e.Env.GetAsync(Trend + "?period=30d", admin)).Json.GetProperty("buckets").GetArrayLength());
    }

    // ---- I8: export ----

    [Fact]
    public async Task Export_7d_is_a_csv_attachment_whose_rows_equal_the_trend()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        DateTimeOffset t = PinNoon(e);
        string admin = await e.Env.AccessTokenAsync("admin@example.test");
        await e.PaidOrderAsync("TREND-CSV", token: await e.Env.AccessTokenAsync("a@example.test"));
        await e.DrainAsync();

        JsonElement trend = (await e.Env.GetAsync(Trend + "?period=7d", admin)).Json;
        using HttpResponseMessage response = await RawGetAsync(e, Export + "?period=7d", admin);
        string csv = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("dashboard-7d-" + t.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv", response.Content.Headers.ContentDisposition.FileName);
        string[] lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("bucket_start_utc,revenue,orders,currency", lines[0]);
        Assert.Equal(8, lines.Length);
        string[] revenues = [.. trend.GetProperty("buckets").EnumerateArray().Select(b => b.GetProperty("revenue").GetProperty("amount").GetString()!)];
        Assert.Equal(revenues, lines.Skip(1).Select(l => l.Split(',')[1]));
    }

    // ---- I9: settings ----

    [Fact]
    public async Task Settings_for_an_Admin_has_exactly_the_currency_and_the_low_stock_threshold()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);

        IdentityTestEnv.Api r = await e.Env.GetAsync(Settings, e.R.Admin);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        Assert.Equal(["currency", "lowStockThreshold"], r.Json.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("USD", r.Str("currency"));
        Assert.Equal(3, r.Json.GetProperty("lowStockThreshold").GetInt32());
    }
}
