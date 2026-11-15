using System.Net;
using System.Text.Json;
using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests.Identity;

// AE1 .. AE6: what the auth endpoints answer, pinned so the move from controller to Minimal API cannot change it.
public class AuthEndpointContractTests(PostgresFixture postgres)
{
    private const string Reg = "/api/v1/auth/register";
    private const string Login = "/api/v1/auth/login";
    private const string Refresh = "/api/v1/auth/refresh";
    private const string Logout = "/api/v1/auth/logout";
    private const string Me = "/api/v1/auth/me";
    private const string Section1551 = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string Section1552 = "https://tools.ietf.org/html/rfc9110#section-15.5.2";
    private static readonly (string, string) CookieMode = ("X-Auth-Transport", "cookie");

    private static void AssertNoStore(IdentityTestEnv.Api r)
    {
        Assert.Equal("no-store, no-cache", r.Response.Headers.CacheControl?.ToString());
        Assert.Equal("no-cache", string.Join(",", r.Response.Headers.Pragma));
    }

    private static void AssertProblem(IdentityTestEnv.Api r, HttpStatusCode status, string type)
    {
        Assert.Equal(status, r.Status);
        Assert.Equal("application/problem+json", r.Response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(type, r.Json.GetProperty("type").GetString());
        Assert.Equal("id-test-1", r.Json.GetProperty("correlationId").GetString());
    }

    private static string[] SetCookies(IdentityTestEnv.Api r) =>
        r.Response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values) ? values.ToArray() : [];

    [Fact]
    public async Task AE1_every_handler_response_is_no_store_including_its_own_4xx()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);

        IdentityTestEnv.Api created = await env.PostAsync(Reg, new { email = "ana@example.test", password = IdentityTestEnv.Password, displayName = "Ana" });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        AssertNoStore(created);
        Assert.Null(created.Response.Headers.Location);

        IdentityTestEnv.Api shortPassword = await env.PostAsync(Reg, new { email = "bo@example.test", password = "short", displayName = "Bo" });
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.Status);
        AssertNoStore(shortPassword);

        IdentityTestEnv.Api duplicate = await env.PostAsync(Reg, new { email = "ana@example.test", password = IdentityTestEnv.Password, displayName = "Ana" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.Status);
        AssertNoStore(duplicate);

        IdentityTestEnv.Api badLogin = await env.LoginAsync("ana@example.test", "not the password");
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.Status);
        AssertNoStore(badLogin);

        IdentityTestEnv.Api refreshed = await env.PostAsync(Refresh, new { refreshToken = created.Str("refreshToken") });
        Assert.Equal(HttpStatusCode.OK, refreshed.Status);
        AssertNoStore(refreshed);

        IdentityTestEnv.Api badRefresh = await env.PostAsync(Refresh, new { refreshToken = "garbage" });
        Assert.Equal(HttpStatusCode.Unauthorized, badRefresh.Status);
        AssertNoStore(badRefresh);

        IdentityTestEnv.Api loggedOut = await env.PostAsync(Logout, new { refreshToken = refreshed.Str("refreshToken") });
        Assert.Equal(HttpStatusCode.NoContent, loggedOut.Status);
        AssertNoStore(loggedOut);

        IdentityTestEnv.Api reset = await env.PostAsync("/api/v1/auth/password-reset", new { email = "ana@example.test" });
        Assert.Equal(HttpStatusCode.Accepted, reset.Status);
        AssertNoStore(reset);
        Assert.Null(reset.Response.Headers.Location);
        Assert.Equal("", reset.Text);

        IdentityTestEnv.Api badConfirm = await env.PostAsync("/api/v1/auth/password-reset/confirm", new { token = "not-a-real-token", newPassword = IdentityTestEnv.Password });
        Assert.Equal(HttpStatusCode.BadRequest, badConfirm.Status);
        Assert.Equal("Reset link not valid", badConfirm.Str("title"));
        AssertNoStore(badConfirm);
    }

    [Fact]
    public async Task AE2_wrong_password_unknown_email_and_a_locked_account_get_one_exact_answer_and_no_cookie()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        await env.AddUserAsync("locked@example.test");

        IdentityTestEnv.Api wrong = await env.LoginAsync("ana@example.test", "not the password");
        IdentityTestEnv.Api unknown = await env.LoginAsync("nobody@example.test", "not the password");
        for (int i = 0; i < 5; i++)
        {
            await env.LoginAsync("locked@example.test", "not the password");
        }
        IdentityTestEnv.Api locked = await env.LoginAsync("locked@example.test");

        foreach (IdentityTestEnv.Api r in new[] { wrong, unknown, locked })
        {
            AssertProblem(r, HttpStatusCode.Unauthorized, Section1552);
            Assert.Equal("Login failed", r.Str("title"));
            Assert.Equal("Email or password is incorrect, or the account is temporarily locked.", r.Str("detail"));
            Assert.Empty(SetCookies(r));
        }
    }

    [Fact]
    public async Task AE3_me_needs_a_signed_in_user_and_a_token_for_a_missing_user_is_401_no_store()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);

        IdentityTestEnv.Api anonymous = await env.GetAsync(Me);
        AssertProblem(anonymous, HttpStatusCode.Unauthorized, Section1552);
        Assert.Contains("Bearer", anonymous.Response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);

        DateTimeOffset now = env.Clock.UtcNow;
        User unsaved = User.Register(Guid.CreateVersion7(now), "ghost@example.test", "Ghost", "hash", "stamp", now);
        string token = env.Factory.Services.GetRequiredService<IAccessTokenIssuer>().Issue(unsaved, now).Token;
        IdentityTestEnv.Api ghost = await env.GetAsync(Me, token);
        AssertProblem(ghost, HttpStatusCode.Unauthorized, Section1552);
        AssertNoStore(ghost);
    }

    [Fact]
    public async Task AE4_validation_failures_are_400_problems_with_the_field_message()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres, countHashing: true);

        IdentityTestEnv.Api register = await env.PostAsync(Reg, new { email = "ana@example.test", password = "short", displayName = "Ana" });
        AssertProblem(register, HttpStatusCode.BadRequest, Section1551);
        Assert.Equal("One or more validation errors occurred.", register.Str("title"));
        Assert.Equal(["Password must be at least 8 characters."], register.Json.GetProperty("errors").GetProperty("password").EnumerateArray().Select(e => e.GetString()));
        Assert.False(string.IsNullOrEmpty(register.Str("traceId")));

        IdentityTestEnv.Api login = await env.PostAsync(Login, new { email = "not-an-email", password = "x" });
        AssertProblem(login, HttpStatusCode.BadRequest, Section1551);
        Assert.Contains("Enter a valid email address.", login.Json.GetProperty("errors").GetProperty("email").EnumerateArray().Select(e => e.GetString()));

        foreach (string url in new[] { Login, Reg })
        {
            IdentityTestEnv.Api empty = await env.SendAsync(HttpMethod.Post, url, "{}");
            AssertProblem(empty, HttpStatusCode.BadRequest, Section1551);
            Assert.NotEmpty(empty.Json.GetProperty("errors").EnumerateObject());
        }
        Assert.Empty(await env.Db.Set<User>().AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, env.Counter!.Verifies);
    }

    [Fact]
    public async Task AE5_cookie_transport_flags_clearing_on_failure_and_reuse_and_no_cookie_in_body_mode()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(
            postgres, settings: new Dictionary<string, string> { ["Identity:RefreshTokens:Cookie:Secure"] = "true" });
        await env.AddUserAsync("ana@example.test");

        IdentityTestEnv.Api login = await env.PostAsync(Login, new { email = "ana@example.test", password = IdentityTestEnv.Password }, null, CookieMode);
        string setCookie = Assert.Single(SetCookies(login));
        foreach (string flag in new[] { "secure", "httponly", "samesite=strict", "path=/api/v1/auth", "expires=" })
        {
            Assert.Contains(flag, setCookie, StringComparison.OrdinalIgnoreCase);
        }
        string first = setCookie.Split(';')[0];

        IdentityTestEnv.Api garbage = await env.PostAsync(Refresh, new { }, null, CookieMode, ("Cookie", "ecommerce_rt=garbage"));
        Assert.Equal(HttpStatusCode.Unauthorized, garbage.Status);
        string cleared = Assert.Single(SetCookies(garbage));
        Assert.StartsWith("ecommerce_rt=", cleared, StringComparison.Ordinal);
        Assert.Contains("1970", cleared, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, (await env.PostAsync(Refresh, new { }, null, CookieMode, ("Cookie", first))).Status);
        IdentityTestEnv.Api replay = await env.PostAsync(Refresh, new { }, null, CookieMode, ("Cookie", first));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.Status);
        Assert.Contains("1970", Assert.Single(SetCookies(replay)), StringComparison.Ordinal);
        RefreshTokenFamily family = await env.Db.Set<RefreshTokenFamily>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RefreshTokenRevocationReason.Reuse, family.RevokedReason);

        IdentityTestEnv.Api bodyRefresh = await env.PostAsync(Refresh, new { refreshToken = "garbage" });
        Assert.Equal(HttpStatusCode.Unauthorized, bodyRefresh.Status);
        Assert.Empty(SetCookies(bodyRefresh));
        IdentityTestEnv.Api bodyLogout = await env.PostAsync(Logout, new { refreshToken = "garbage" });
        Assert.Equal(HttpStatusCode.NoContent, bodyLogout.Status);
        Assert.Empty(SetCookies(bodyLogout));
    }

    [Fact]
    public async Task AE6_logout_and_refresh_still_work_when_the_client_sends_no_body()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");

        Assert.Equal(HttpStatusCode.NoContent, (await env.SendAsync(HttpMethod.Post, Logout, null)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await env.SendAsync(HttpMethod.Post, Logout, "null")).Status);

        IdentityTestEnv.Api login = await env.PostAsync(Login, new { email = "ana@example.test", password = IdentityTestEnv.Password }, null, CookieMode);
        string cookie = Assert.Single(SetCookies(login)).Split(';')[0];
        IdentityTestEnv.Api refreshed = await env.SendAsync(HttpMethod.Post, Refresh, null, null, CookieMode, ("Cookie", cookie));
        Assert.Equal(HttpStatusCode.OK, refreshed.Status);
        Assert.Equal(JsonValueKind.String, refreshed.Json.GetProperty("accessToken").ValueKind);
    }
}
