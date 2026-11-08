using System.Net;
using System.Text;
using Ecommerce.Identity.Domain;
using Ecommerce.Notifications.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Api.IntegrationTests.Identity;

// II-9: password reset request and confirm.
public class PasswordResetTests(PostgresFixture postgres)
{
    private const string RequestUrl = "/api/v1/auth/password-reset";
    private const string ConfirmUrl = "/api/v1/auth/password-reset/confirm";
    private const string NewPassword = "a brand new passphrase";

    [Fact]
    public async Task II9_request_answers_202_identically_and_sends_once_only_for_a_real_account()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");

        IdentityTestEnv.Api unknown = await env.PostAsync(RequestUrl, new { email = "nobody@example.test" });
        IdentityTestEnv.Api known = await env.PostAsync(RequestUrl, new { email = "ana@example.test" });

        Assert.Equal(HttpStatusCode.Accepted, unknown.Status);
        Assert.Equal(HttpStatusCode.Accepted, known.Status);
        Assert.Equal(unknown.Text, known.Text);
        Assert.Equal(1, await env.DrainOutboxAsync());
        Assert.Equal("", known.Text);
        EmailMessage sent = Assert.Single(env.Sender.Sent);
        Assert.Equal("ana@example.test", sent.To);

        // The token row holds a 32-byte hash, never the token itself.
        PasswordResetToken row = await env.Db.Set<PasswordResetToken>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(32, row.TokenHash.Length);
        Assert.NotEqual(Encoding.UTF8.GetBytes(env.Sender.TokenOf(0)), row.TokenHash);
    }

    [Fact]
    public async Task II9_confirm_changes_the_password_revokes_every_session_clears_lockout_and_is_single_use()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        string oldRefresh = (await env.LoginAsync("ana@example.test")).Str("refreshToken");
        for (int i = 0; i < 5; i++)
        {
            await env.LoginAsync("ana@example.test", "wrong guess");
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.LoginAsync("ana@example.test")).Status); // locked

        await env.PostAsync(RequestUrl, new { email = "ana@example.test" });
        await env.DrainOutboxAsync();
        string token = env.Sender.TokenOf(0);

        Assert.Equal(HttpStatusCode.NoContent, (await env.PostAsync(ConfirmUrl, new { token, newPassword = NewPassword })).Status);

        Assert.Equal(HttpStatusCode.OK, (await env.LoginAsync("ana@example.test", NewPassword)).Status); // lockout cleared
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.LoginAsync("ana@example.test")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.PostAsync("/api/v1/auth/refresh", new { refreshToken = oldRefresh })).Status);
        IdentityTestEnv.Api again = await env.PostAsync(ConfirmUrl, new { token, newPassword = "yet another passphrase" });
        Assert.Equal(HttpStatusCode.BadRequest, again.Status);
        Assert.Equal("This reset link is invalid or has expired.", again.Str("detail"));
    }

    [Fact]
    public async Task II9_a_token_at_its_30_minute_expiry_and_an_unknown_token_are_rejected()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        await env.PostAsync(RequestUrl, new { email = "ana@example.test" });
        await env.DrainOutboxAsync();
        string token = env.Sender.TokenOf(0);

        env.Clock.UtcNow = env.Clock.UtcNow.AddMinutes(30);

        Assert.Equal(HttpStatusCode.BadRequest, (await env.PostAsync(ConfirmUrl, new { token, newPassword = NewPassword })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await env.PostAsync(ConfirmUrl, new { token = "not-a-real-token", newPassword = NewPassword })).Status);
    }

    [Fact]
    public async Task II9_a_newer_request_invalidates_the_older_token()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        await env.PostAsync(RequestUrl, new { email = "ana@example.test" });
        await env.DrainOutboxAsync();
        await env.PostAsync(RequestUrl, new { email = "ana@example.test" });
        await env.DrainOutboxAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await env.PostAsync(ConfirmUrl, new { token = env.Sender.TokenOf(0), newPassword = NewPassword })).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await env.PostAsync(ConfirmUrl, new { token = env.Sender.TokenOf(1), newPassword = NewPassword })).Status);
    }

    [Fact]
    public async Task II9_two_parallel_confirms_of_one_token_produce_one_204()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        await env.PostAsync(RequestUrl, new { email = "ana@example.test" });
        await env.DrainOutboxAsync();
        string token = env.Sender.TokenOf(0);

        IdentityTestEnv.Api[] results = await Task.WhenAll(
            env.PostAsync(ConfirmUrl, new { token, newPassword = NewPassword }),
            env.PostAsync(ConfirmUrl, new { token, newPassword = "second passphrase here" }));

        Assert.Single(results, r => r.Status == HttpStatusCode.NoContent);
        Assert.Single(results, r => r.Status == HttpStatusCode.BadRequest);
    }
}
