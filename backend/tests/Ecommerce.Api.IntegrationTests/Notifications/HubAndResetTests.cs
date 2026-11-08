using System.Net;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Refunds;
using Ecommerce.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;

namespace Ecommerce.Api.IntegrationTests.Notifications;

// SignalR (Slice 6 D16): authenticated connections only, the token travels in access_token for /hubs only, and never reaches a log.
public class HubTests(PostgresFixture postgres)
{
    private const string Negotiate = "/hubs/orders/negotiate?negotiateVersion=1";

    [Fact]
    public async Task An_unauthenticated_connect_is_rejected_a_valid_token_in_access_token_is_accepted()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        string token = await env.AccessTokenAsync("ana@example.test");

        Assert.Equal(HttpStatusCode.Unauthorized, (await env.SendAsync(HttpMethod.Post, Negotiate)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.SendAsync(HttpMethod.Post, Negotiate + "&access_token=not.a.jwt")).Status);
        IdentityTestEnv.Api ok = await env.SendAsync(HttpMethod.Post, Negotiate + "&access_token=" + token);
        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.True(ok.Json.TryGetProperty("connectionToken", out _));
    }

    [Fact]
    public async Task The_access_token_query_string_is_honoured_only_on_hub_paths()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        string token = await env.AccessTokenAsync("ana@example.test");

        Assert.Equal(HttpStatusCode.Unauthorized, (await env.GetAsync("/api/v1/auth/me?access_token=" + token)).Status);
        Assert.Equal(HttpStatusCode.OK, (await env.GetAsync("/api/v1/auth/me", token)).Status);
    }

    [Fact]
    public async Task No_log_line_contains_the_access_token_from_a_hub_request()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        string token = await env.AccessTokenAsync("ana@example.test");

        await env.SendAsync(HttpMethod.Post, Negotiate + "&access_token=" + token);
        await env.SendAsync(HttpMethod.Post, Negotiate + "&access_token=" + token.Substring(0, token.Length - 3) + "bad");

        Assert.Contains(env.Logs.Events, e => e.RenderMessage().Contains("/hubs/orders"));
        foreach (LogEvent e in env.Logs.Events)
        {
            string text = e.RenderMessage() + " " + string.Join(" ", e.Properties.Values.Select(v => v.ToString())) + " " + e.Exception;
            Assert.DoesNotContain(token.Substring(0, 40), text);
            Assert.DoesNotContain("access_token", text);
        }
    }
}

// Password reset through the outbox (Slice 6 D15).
public class PasswordResetOutboxTests(PostgresFixture postgres)
{
    private const string RequestUrl = "/api/v1/auth/password-reset";

    private static async Task<long> EventsAsync(IdentityTestEnv env) =>
        await env.Db.Database.SqlQueryRaw<long>("SELECT count(*) AS \"Value\" FROM outbox_events WHERE event_type = 'PasswordResetRequested'").SingleAsync(TestContext.Current.CancellationToken);

    private static async Task<long> TokensAsync(IdentityTestEnv env) =>
        await env.Db.Set<PasswordResetToken>().AsNoTracking().LongCountAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_known_email_stages_one_event_and_no_token_until_the_consumer_runs_an_unknown_email_stages_nothing()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");

        Assert.Equal(HttpStatusCode.Accepted, (await env.PostAsync(RequestUrl, new { email = "nobody@example.test" })).Status);
        Assert.Equal(0, await EventsAsync(env));
        Assert.Equal(HttpStatusCode.Accepted, (await env.PostAsync(RequestUrl, new { email = "ana@example.test" })).Status);

        Assert.Equal(1, await EventsAsync(env));
        Assert.Equal(0, await TokensAsync(env));
        Assert.Empty(env.Sender.Sent);
        string payload = await env.Db.Database.SqlQueryRaw<string>("SELECT payload::text AS \"Value\" FROM outbox_events WHERE event_type = 'PasswordResetRequested'").SingleAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("token", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_retry_after_a_send_failure_mints_a_new_token_and_only_one_mail_is_kept()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        await env.PostAsync(RequestUrl, new { email = "ana@example.test" });
        env.Sender.FailNext(1);

        await env.DrainOutboxAsync();
        Assert.Empty(env.Sender.Sent);
        Assert.Equal(1, await TokensAsync(env)); // minted, stored as a hash, but never delivered
        env.Clock.UtcNow = env.Clock.UtcNow.AddSeconds(2);
        await env.DrainOutboxAsync();

        Assert.Equal(2, await TokensAsync(env));
        Assert.Single(env.Sender.Sent);
        Assert.Equal(HttpStatusCode.NoContent,
            (await env.PostAsync("/api/v1/auth/password-reset/confirm", new { token = env.Sender.TokenOf(0), newPassword = "a brand new passphrase" })).Status);
    }

    [Fact]
    public async Task Two_requests_before_the_worker_runs_leave_only_the_newest_link_valid()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        await env.PostAsync(RequestUrl, new { email = "ana@example.test" });
        env.Clock.UtcNow = env.Clock.UtcNow.AddSeconds(1);
        await env.PostAsync(RequestUrl, new { email = "ana@example.test" });

        await env.DrainOutboxAsync();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await env.PostAsync("/api/v1/auth/password-reset/confirm", new { token = env.Sender.TokenOf(0), newPassword = "a brand new passphrase" })).Status);
        Assert.Equal(HttpStatusCode.NoContent,
            (await env.PostAsync("/api/v1/auth/password-reset/confirm", new { token = env.Sender.TokenOf(1), newPassword = "a brand new passphrase" })).Status);
    }
}

// Admin order detail (D14) and Test 29's refunds[].
public class AdminOrderDetailTests(PostgresFixture postgres)
{
    [Fact]
    public async Task T29_the_customer_detail_lists_refunds_in_order_and_the_admin_detail_shows_the_servers_totals()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        Guid first = RefundEnv.RefundIdOf(await e.RefundAsync(order, "10.00"));
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddMinutes(1);
        Guid second = RefundEnv.RefundIdOf(await e.RefundAsync(order, "5.00"));
        await e.RefundWebhookAsync(first, "succeeded");

        IdentityTestEnv.Api customer = await e.Env.GetAsync($"/api/v1/orders/{order}", e.TokenA);
        IdentityTestEnv.Api admin = await e.Env.GetAsync($"/api/v1/admin/orders/{order}", e.Admin);

        Assert.Equal(HttpStatusCode.OK, customer.Status);
        System.Text.Json.JsonElement refunds = customer.Json.GetProperty("refunds");
        Assert.Equal(2, refunds.GetArrayLength());
        Assert.Equal(("10.00", "Succeeded"), (refunds[0].GetProperty("amount").GetProperty("amount").GetString(), refunds[0].GetProperty("status").GetString()));
        Assert.Equal(("5.00", "Pending"), (refunds[1].GetProperty("amount").GetProperty("amount").GetString(), refunds[1].GetProperty("status").GetString()));
        Assert.True(refunds[0].TryGetProperty("createdAt", out _));
        Assert.False(refunds[0].TryGetProperty("reason", out _));
        Assert.False(refunds[0].TryGetProperty("actorId", out _));

        Assert.Equal(HttpStatusCode.OK, admin.Status);
        Assert.Equal(e.CustomerId, admin.Json.GetProperty("userId").GetGuid());
        Assert.Equal("10.00", admin.Json.GetProperty("refundedTotal").GetProperty("amount").GetString());
        Assert.Equal("5.00", admin.Json.GetProperty("pendingTotal").GetProperty("amount").GetString());
        Assert.Equal("10.00", admin.Json.GetProperty("remaining").GetProperty("amount").GetString());
        Assert.Equal([first, second], admin.Json.GetProperty("refunds").EnumerateArray().Select(r => r.GetProperty("refundId").GetGuid()));
        Assert.Equal(order, admin.Json.GetProperty("order").GetProperty("id").GetGuid());
        Assert.Equal(0, admin.Json.GetProperty("returnRequests").GetArrayLength());
    }

    [Fact]
    public async Task The_admin_detail_is_open_to_Admin_and_Support_only_and_unknown_is_404()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync();
        string url = $"/api/v1/admin/orders/{order}";

        Assert.Equal(HttpStatusCode.OK, (await e.Env.GetAsync(url, e.Admin)).Status);
        Assert.Equal(HttpStatusCode.OK, (await e.Env.GetAsync(url, e.Support)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.GetAsync(url, e.TokenA)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await e.Env.GetAsync(url, null)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await e.Env.GetAsync($"/api/v1/admin/orders/{Guid.NewGuid()}", e.Admin)).Status);
    }

    [Fact]
    public async Task An_order_with_no_payment_shows_zero_totals_and_no_refunds()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await Orders.OrderSeed.InsertAsync(e.Cs, e.CustomerId, "PendingPayment", e.Env.Clock.UtcNow);

        IdentityTestEnv.Api admin = await e.Env.GetAsync($"/api/v1/admin/orders/{order}", e.Admin);

        Assert.Equal("0.00", admin.Json.GetProperty("remaining").GetProperty("amount").GetString());
        Assert.Equal(0, admin.Json.GetProperty("refunds").GetArrayLength());
        Assert.Equal(0, (await e.Env.GetAsync($"/api/v1/orders/{order}", e.TokenA)).Json.GetProperty("refunds").GetArrayLength());
    }

    [Fact]
    public async Task The_admin_detail_includes_the_return_requests_of_the_order()
    {
        await using RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync("Delivered");
        Guid variant = await e.VariantOfAsync(order);
        await e.Env.PostAsync($"/api/v1/orders/{order}/return-request", new { items = new[] { new { variantId = variant, quantity = 1 } }, reason = "Too small" }, e.TokenA);

        IdentityTestEnv.Api admin = await e.Env.GetAsync($"/api/v1/admin/orders/{order}", e.Admin);

        System.Text.Json.JsonElement request = Assert.Single(admin.Json.GetProperty("returnRequests").EnumerateArray());
        Assert.Equal(("Requested", "Too small"), (request.GetProperty("status").GetString(), request.GetProperty("reason").GetString()));
    }
}
