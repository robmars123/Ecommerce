using System.Net;
using Ecommerce.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;

namespace Ecommerce.Api.IntegrationTests.Identity;

// II-1 .. II-8: register, login, lockout, refresh rotation and reuse, expiry, logout, cookie transport.
public class AuthFlowTests(PostgresFixture postgres)
{
    private const string Reg = "/api/v1/auth/register";
    private const string Refresh = "/api/v1/auth/refresh";

    [Fact]
    public async Task II1_register_creates_a_customer_with_tokens_and_a_hashed_password()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);

        IdentityTestEnv.Api r = await env.PostAsync(Reg, new { email = "ana@example.test", password = IdentityTestEnv.Password, displayName = "Ana" });

        Assert.Equal(HttpStatusCode.Created, r.Status);
        Assert.False(string.IsNullOrEmpty(r.Str("accessToken")));
        Assert.False(string.IsNullOrEmpty(r.Str("refreshToken")));
        Assert.Equal("Customer", r.Json.GetProperty("user").GetProperty("role").GetString());
        Assert.Contains("no-store", string.Join(",", r.Response.Headers.CacheControl?.ToString() ?? ""));
        User user = await env.Db.Set<User>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(IdentityTestEnv.Password, user.PasswordHash);
        Assert.StartsWith("AQAAAA", user.PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task II1_extra_role_member_is_400_and_creates_no_row_and_email_case_duplicates_are_409()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        CancellationToken ct = TestContext.Current.CancellationToken;

        IdentityTestEnv.Api extra = await env.PostAsync(Reg, """{"email":"x@example.test","password":"correct horse battery","displayName":"X","role":"Admin"}""");
        Assert.Equal(HttpStatusCode.BadRequest, extra.Status);
        Assert.Empty(await env.Db.Set<User>().AsNoTracking().ToListAsync(ct));

        Assert.Equal(HttpStatusCode.Created, (await env.PostAsync(Reg, new { email = "ana@example.test", password = IdentityTestEnv.Password, displayName = "Ana" })).Status);
        IdentityTestEnv.Api dup = await env.PostAsync(Reg, new { email = "Ana@Example.TEST", password = IdentityTestEnv.Password, displayName = "Ana 2" });
        Assert.Equal(HttpStatusCode.Conflict, dup.Status);
        Assert.Single(await env.Db.Set<User>().AsNoTracking().ToListAsync(ct));
    }

    [Fact]
    public async Task II2_wrong_password_and_unknown_email_are_indistinguishable_and_email_case_is_ignored()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");

        IdentityTestEnv.Api wrong = await env.LoginAsync("ana@example.test", "not the password");
        IdentityTestEnv.Api unknown = await env.LoginAsync("nobody@example.test", "not the password");

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.Status);
        Assert.Equal(wrong.Status, unknown.Status);
        Assert.Equal(wrong.Str("title"), unknown.Str("title"));
        Assert.Equal(wrong.Str("detail"), unknown.Str("detail"));
        Assert.Equal(HttpStatusCode.OK, (await env.LoginAsync("ANA@Example.Test")).Status);
    }

    [Fact]
    public async Task II3_ten_parallel_wrong_guesses_verify_exactly_five_passwords_then_lock_until_five_minutes_pass()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres, countHashing: true);
        await env.AddUserAsync("ana@example.test");

        IdentityTestEnv.Api[] results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => env.LoginAsync("ana@example.test", "wrong guess")));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.Unauthorized, r.Status));
        Assert.Equal(5, env.Counter!.Verifies);
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.LoginAsync("ana@example.test")).Status);
        Assert.Equal(5, env.Counter.Verifies);

        env.Clock.UtcNow = env.Clock.UtcNow.AddMinutes(5);
        Assert.Equal(HttpStatusCode.OK, (await env.LoginAsync("ana@example.test")).Status);
    }

    [Fact]
    public async Task II4_replaying_a_used_refresh_token_is_401_revokes_the_family_and_logs_a_warning_without_the_token()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        User user = await env.AddUserAsync("ana@example.test");
        string first = (await env.LoginAsync("ana@example.test")).Str("refreshToken");

        IdentityTestEnv.Api rotated = await env.PostAsync(Refresh, new { refreshToken = first });
        Assert.Equal(HttpStatusCode.OK, rotated.Status);
        string child = rotated.Str("refreshToken");

        Assert.Equal(HttpStatusCode.Unauthorized, (await env.PostAsync(Refresh, new { refreshToken = first })).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.PostAsync(Refresh, new { refreshToken = child })).Status);

        RefreshTokenFamily family = await env.Db.Set<RefreshTokenFamily>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RefreshTokenRevocationReason.Reuse, family.RevokedReason);
        LogEvent warning = Assert.Single(env.Logs.Events, e => e.Level == Serilog.Events.LogEventLevel.Warning && e.MessageTemplate.Text.Contains("Refresh token reuse detected"));
        string text = warning.RenderMessage();
        Assert.Contains(family.Id.ToString(), text);
        Assert.Contains(user.Id.ToString(), text);
        Assert.DoesNotContain(first, text);
        Assert.DoesNotContain(child, text);
    }

    [Fact]
    public async Task II5_ten_parallel_refreshes_of_one_token_produce_one_success_and_a_dead_family()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        string token = (await env.LoginAsync("ana@example.test")).Str("refreshToken");

        IdentityTestEnv.Api[] results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => env.PostAsync(Refresh, new { refreshToken = token })));

        List<IdentityTestEnv.Api> winners = results.Where(r => r.Status == HttpStatusCode.OK).ToList();
        Assert.Single(winners);
        Assert.Equal(9, results.Count(r => r.Status == HttpStatusCode.Unauthorized));
        CancellationToken ct = TestContext.Current.CancellationToken;
        Assert.True(await env.Db.Set<RefreshToken>().AsNoTracking().CountAsync(ct) <= 2);
        Assert.Equal(RefreshTokenRevocationReason.Reuse, (await env.Db.Set<RefreshTokenFamily>().AsNoTracking().SingleAsync(ct)).RevokedReason);
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.PostAsync(Refresh, new { refreshToken = winners[0].Str("refreshToken") })).Status);
    }

    [Fact]
    public async Task II6_a_token_idle_past_14_days_is_401_without_revoking_and_the_family_dies_at_30_days()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        CancellationToken ct = TestContext.Current.CancellationToken;

        string idle = (await env.LoginAsync("ana@example.test")).Str("refreshToken");
        env.Clock.UtcNow = env.Clock.UtcNow.AddDays(14).AddMinutes(1);
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.PostAsync(Refresh, new { refreshToken = idle })).Status);
        Assert.Null((await env.Db.Set<RefreshTokenFamily>().AsNoTracking().SingleAsync(ct)).RevokedAt);

        string token = (await env.LoginAsync("ana@example.test")).Str("refreshToken");
        for (int i = 0; i < 2; i++)
        {
            env.Clock.UtcNow = env.Clock.UtcNow.AddDays(13);
            IdentityTestEnv.Api r = await env.PostAsync(Refresh, new { refreshToken = token });
            Assert.Equal(HttpStatusCode.OK, r.Status);
            token = r.Str("refreshToken");
        }
        env.Clock.UtcNow = env.Clock.UtcNow.AddDays(5);
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.PostAsync(Refresh, new { refreshToken = token })).Status);
    }

    [Fact]
    public async Task II7_logout_revokes_and_is_204_every_time_even_for_garbage()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        string token = (await env.LoginAsync("ana@example.test")).Str("refreshToken");

        Assert.Equal(HttpStatusCode.NoContent, (await env.PostAsync("/api/v1/auth/logout", new { refreshToken = token })).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.PostAsync(Refresh, new { refreshToken = token })).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await env.PostAsync("/api/v1/auth/logout", new { refreshToken = token })).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await env.PostAsync("/api/v1/auth/logout", new { refreshToken = "garbage" })).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await env.PostAsync("/api/v1/auth/logout")).Status);
    }

    [Fact]
    public async Task II8_cookie_transport_keeps_the_token_out_of_the_body_and_requires_the_header()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        (string, string) cookieMode = ("X-Auth-Transport", "cookie");

        IdentityTestEnv.Api login = await env.PostAsync("/api/v1/auth/login", new { email = "ana@example.test", password = IdentityTestEnv.Password }, null, cookieMode);
        Assert.Equal(HttpStatusCode.OK, login.Status);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, login.Json.GetProperty("refreshToken").ValueKind);
        string setCookie = Assert.Single(login.Response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("ecommerce_rt=", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        string cookie = setCookie.Split(';')[0];

        IdentityTestEnv.Api noHeader = await env.PostAsync(Refresh, new { }, null, ("Cookie", cookie));
        Assert.Equal(HttpStatusCode.Unauthorized, noHeader.Status);

        IdentityTestEnv.Api withHeader = await env.PostAsync(Refresh, new { }, null, cookieMode, ("Cookie", cookie));
        Assert.Equal(HttpStatusCode.OK, withHeader.Status);
        string rotated = Assert.Single(withHeader.Response.Headers.GetValues("Set-Cookie")).Split(';')[0];
        Assert.NotEqual(cookie, rotated);

        IdentityTestEnv.Api logout = await env.PostAsync("/api/v1/auth/logout", new { }, null, cookieMode, ("Cookie", rotated));
        Assert.Equal(HttpStatusCode.NoContent, logout.Status);
        Assert.Contains("1970", Assert.Single(logout.Response.Headers.GetValues("Set-Cookie")), StringComparison.Ordinal);
    }
}
