using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;

namespace Ecommerce.Identity.Tests.Application;

// ID-7: the reset request/confirm flows, the post-commit sender (rule 6), and the revocation side effects.
public class PasswordResetServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Request_for_an_unknown_email_sends_nothing_and_creates_nothing()
    {
        IdentityFakes f = new IdentityFakes();

        await f.PasswordReset().RequestAsync("nobody@example.test", Ct);

        Assert.Empty(f.Sender.Sent);
        Assert.Empty(f.ResetTokens.Tokens);
        Assert.Equal(0, f.ResetTokens.InvalidateCalls);
    }

    [Fact]
    public async Task Request_for_a_known_email_invalidates_old_tokens_and_adds_one_inside_the_transaction_then_sends_after_commit()
    {
        IdentityFakes f = new IdentityFakes();
        User user = f.AddUser();

        await f.PasswordReset().RequestAsync(" ana@example.test ", Ct);

        PasswordResetToken token = Assert.Single(f.ResetTokens.Tokens);
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(IdentityFakes.T0 + IdentityFakes.Lifetimes.PasswordReset, token.ExpiresAt);

        int begin = f.Log.IndexOf("tx-begin");
        int invalidate = f.Log.IndexOf("reset-tokens-invalidated");
        int add = f.Log.IndexOf("reset-token-added");
        int commit = f.Log.IndexOf("tx-commit");
        int send = f.Log.IndexOf("send-reset-link");
        Assert.True(begin < invalidate && invalidate < add && add < commit && commit < send, string.Join(",", f.Log));

        (Guid UserId, string Email, string? Link) sent = Assert.Single(f.Sender.Sent);
        Assert.Equal(user.Id, sent.UserId);
        Assert.Equal("ana@example.test", sent.Email);
    }

    [Fact]
    public async Task The_link_carries_the_plaintext_token_but_the_database_holds_only_its_hash()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();

        await f.PasswordReset("https://app.test/reset-password").RequestAsync("ana@example.test", Ct);

        string? link = Assert.Single(f.Sender.Sent).Link;
        Assert.StartsWith("https://app.test/reset-password?token=", link);
        string plaintext = link!["https://app.test/reset-password?token=".Length..];
        Assert.Equal(TokenHash.Of(plaintext), Assert.Single(f.ResetTokens.Tokens).TokenHash);
    }

    [Fact]
    public async Task With_no_link_base_url_configured_the_sender_gets_no_link()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();

        await f.PasswordReset(linkBaseUrl: "").RequestAsync("ana@example.test", Ct);

        Assert.Null(Assert.Single(f.Sender.Sent).Link);
    }

    [Fact]
    public async Task A_newer_request_invalidates_the_older_token()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();
        PasswordResetService service = f.PasswordReset();

        await service.RequestAsync("ana@example.test", Ct);
        await service.RequestAsync("ana@example.test", Ct);

        Assert.Equal(2, f.ResetTokens.Tokens.Count);
        Assert.NotNull(f.ResetTokens.Tokens[0].InvalidatedAt);
        Assert.Null(f.ResetTokens.Tokens[1].InvalidatedAt);
    }

    private static async Task<string> RequestAndGetToken(IdentityFakes f, PasswordResetService service)
    {
        await service.RequestAsync("ana@example.test", Ct);
        string link = f.Sender.Sent[^1].Link!;
        return link[(link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..];
    }

    [Fact]
    public async Task Confirm_replaces_the_password_revokes_every_family_and_clears_the_lockout()
    {
        IdentityFakes f = new IdentityFakes();
        User user = f.AddUser();
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(IdentityFakes.T0, IdentityFakes.Lockout);
        }
        f.Sessions.StartFamily(user.Id, IdentityFakes.T0);
        f.Sessions.StartFamily(user.Id, IdentityFakes.T0);
        PasswordResetService service = f.PasswordReset();
        string token = await RequestAndGetToken(f, service);
        f.Log.Clear();

        ResetConfirmOutcome outcome = await service.ConfirmAsync(token, "brand new password", Ct);

        Assert.IsType<ResetConfirmOutcome.Success>(outcome);
        Assert.Equal("hash:brand new password", user.PasswordHash);
        Assert.Equal("stamp-1", user.SecurityStamp);
        Assert.False(user.IsLockedOut(IdentityFakes.T0));
        Assert.All(f.RefreshTokens.Families, fam => Assert.Equal(RefreshTokenRevocationReason.PasswordReset, fam.RevokedReason));
        Assert.Equal((user.Id, RefreshTokenRevocationReason.PasswordReset), Assert.Single(f.RefreshTokens.RevokeAllCalls));
        Assert.NotNull(f.ResetTokens.Tokens[0].UsedAt);
        Assert.True(f.Log.IndexOf("hash") < f.Log.IndexOf("tx-begin"), "the password must be hashed before the transaction opens: " + string.Join(",", f.Log));
    }

    [Fact]
    public async Task Confirm_with_an_unknown_token_is_rejected_without_a_transaction()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();

        ResetConfirmOutcome outcome = await f.PasswordReset().ConfirmAsync("not-a-real-token", "brand new password", Ct);

        Assert.IsType<ResetConfirmOutcome.InvalidToken>(outcome);
        Assert.Equal(0, f.UnitOfWork.Transactions);
    }

    [Fact]
    public async Task A_token_works_once()
    {
        IdentityFakes f = new IdentityFakes();
        User user = f.AddUser();
        PasswordResetService service = f.PasswordReset();
        string token = await RequestAndGetToken(f, service);

        Assert.IsType<ResetConfirmOutcome.Success>(await service.ConfirmAsync(token, "first new password", Ct));
        ResetConfirmOutcome second = await service.ConfirmAsync(token, "second new password", Ct);

        Assert.IsType<ResetConfirmOutcome.InvalidToken>(second);
        Assert.Equal("hash:first new password", user.PasswordHash);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        IdentityFakes f = new IdentityFakes();
        User user = f.AddUser();
        PasswordResetService service = f.PasswordReset();
        string token = await RequestAndGetToken(f, service);
        f.Clock.UtcNow = IdentityFakes.T0 + IdentityFakes.Lifetimes.PasswordReset;

        ResetConfirmOutcome outcome = await service.ConfirmAsync(token, "brand new password", Ct);

        Assert.IsType<ResetConfirmOutcome.InvalidToken>(outcome);
        Assert.Equal("hash:correct horse", user.PasswordHash);
    }

    [Fact]
    public async Task An_older_token_is_rejected_after_a_newer_request()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();
        PasswordResetService service = f.PasswordReset();
        string older = await RequestAndGetToken(f, service);
        await RequestAndGetToken(f, service);

        Assert.IsType<ResetConfirmOutcome.InvalidToken>(await service.ConfirmAsync(older, "brand new password", Ct));
    }
}
