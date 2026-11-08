using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;
using Ecommerce.Notifications.Contracts;
using Ecommerce.SharedKernel.Outbox;

namespace Ecommerce.Identity.Tests.Application;

// ID-7: the reset request/confirm flows. Slice 6 D15 moved delivery to the outbox: the request stages an event, the consumer
// mints the token and mails it. (Changed tests: the 1b sender-seam tests now assert the outbox and the consumer instead.)
public class PasswordResetServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Request_for_an_unknown_email_stages_nothing_and_creates_nothing()
    {
        IdentityFakes f = new IdentityFakes();

        await f.PasswordReset().RequestAsync("nobody@example.test", Ct);

        Assert.Empty(f.Outbox.Messages);
        Assert.Empty(f.Sender.Sent);
        Assert.Empty(f.ResetTokens.Tokens);
        Assert.Equal(0, f.ResetTokens.InvalidateCalls);
    }

    [Fact]
    public async Task Request_for_a_known_email_invalidates_old_tokens_and_stages_one_event_inside_the_transaction_and_sends_nothing()
    {
        IdentityFakes f = new IdentityFakes();
        User user = f.AddUser();

        await f.PasswordReset().RequestAsync(" ana@example.test ", Ct);

        OutboxMessage staged = Assert.Single(f.Outbox.Messages);
        Assert.Equal(PasswordResetService.RequestedEvent, staged.EventType);
        Assert.Equal(user.Id, staged.AggregateId);
        Assert.DoesNotContain("token", staged.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(f.ResetTokens.Tokens);
        Assert.Empty(f.Sender.Sent);
        int begin = f.Log.IndexOf("tx-begin");
        int invalidate = f.Log.IndexOf("reset-tokens-invalidated");
        int stage = f.Log.IndexOf("outbox-added");
        int commit = f.Log.IndexOf("tx-commit");
        Assert.True(begin < invalidate && invalidate < stage && stage < commit, string.Join(",", f.Log));
    }

    [Fact]
    public async Task The_consumer_mints_the_token_stores_only_its_hash_in_its_own_transaction_then_mails_the_link_after_commit()
    {
        IdentityFakes f = new IdentityFakes();
        User user = f.AddUser();
        await f.PasswordReset("https://app.test/reset-password").RequestAsync("ana@example.test", Ct);
        f.Log.Clear();

        await f.DeliverAsync("https://app.test/reset-password");

        PasswordResetToken token = Assert.Single(f.ResetTokens.Tokens);
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(IdentityFakes.T0 + IdentityFakes.Lifetimes.PasswordReset, token.ExpiresAt);
        EmailMessage mail = Assert.Single(f.Sender.Sent);
        Assert.Equal("ana@example.test", mail.To);
        string link = f.LinkOf(mail);
        Assert.StartsWith("https://app.test/reset-password?token=", link);
        Assert.Equal(TokenHash.Of(link["https://app.test/reset-password?token=".Length..]), token.TokenHash);
        Assert.True(f.Log.IndexOf("tx-commit") < f.Log.IndexOf("send-email"), string.Join(",", f.Log));
        Assert.EndsWith(f.Outbox.Messages[0].EventId.ToString(), mail.IdempotencyKey);
    }

    [Fact]
    public async Task With_no_link_base_url_configured_the_mail_has_no_link()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();
        await f.PasswordReset(linkBaseUrl: "").RequestAsync("ana@example.test", Ct);

        await f.DeliverAsync("");

        Assert.DoesNotContain("token=", Assert.Single(f.Sender.Sent).TextBody);
    }

    [Fact]
    public async Task A_retry_after_a_send_failure_mints_a_fresh_token_and_reuses_the_idempotency_key()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();
        await f.PasswordReset().RequestAsync("ana@example.test", Ct);
        f.Sender.FailNext = 1;
        PasswordResetConsumer consumer = f.ResetConsumer("https://app.test/reset-password");

        await Assert.ThrowsAsync<EmailProviderUnavailableException>(() => consumer.HandleAsync(f.Outbox.Messages[0], Ct));
        await consumer.HandleAsync(f.Outbox.Messages[0], Ct);

        Assert.Equal(2, f.ResetTokens.Tokens.Count);
        Assert.NotEqual(f.ResetTokens.Tokens[0].TokenHash, f.ResetTokens.Tokens[1].TokenHash);
        Assert.Equal(2, f.Sender.Attempts);
        Assert.Single(f.Sender.Sent);
    }

    [Fact]
    public async Task The_consumer_ignores_a_user_that_no_longer_exists()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();
        await f.PasswordReset().RequestAsync("ana@example.test", Ct);
        f.Users.Users.Clear();

        await f.DeliverAsync("https://app.test/reset-password");

        Assert.Empty(f.Sender.Sent);
        Assert.Empty(f.ResetTokens.Tokens);
    }

    [Fact]
    public async Task A_newer_request_invalidates_the_older_token()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();
        PasswordResetService service = f.PasswordReset();

        await RequestAndGetToken(f, service);
        await RequestAndGetToken(f, service);

        Assert.Equal(2, f.ResetTokens.Tokens.Count);
        Assert.NotNull(f.ResetTokens.Tokens[0].InvalidatedAt);
        Assert.Null(f.ResetTokens.Tokens[1].InvalidatedAt);
    }

    private static async Task<string> RequestAndGetToken(IdentityFakes f, PasswordResetService service)
    {
        await service.RequestAsync("ana@example.test", Ct);
        await f.DeliverAsync("https://app.test/reset-password");
        string link = f.LinkOf(f.Sender.Sent[^1]);
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

    [Fact]
    public async Task Two_requests_then_one_delivery_leave_only_the_newest_token_valid()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();
        PasswordResetService service = f.PasswordReset();
        await service.RequestAsync("ana@example.test", Ct);
        f.Clock.UtcNow = IdentityFakes.T0.AddSeconds(1);
        await service.RequestAsync("ana@example.test", Ct);

        await f.DeliverAsync("https://app.test/reset-password");

        string older = f.TokenOf(0);
        string newer = f.TokenOf(1);
        Assert.IsType<ResetConfirmOutcome.InvalidToken>(await service.ConfirmAsync(older, "brand new password", Ct));
        Assert.IsType<ResetConfirmOutcome.Success>(await service.ConfirmAsync(newer, "brand new password", Ct));
    }

    [Fact]
    public async Task A_stale_event_retried_after_a_newer_one_was_handled_mints_nothing()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();
        PasswordResetService service = f.PasswordReset();
        await service.RequestAsync("ana@example.test", Ct);
        f.Clock.UtcNow = IdentityFakes.T0.AddSeconds(1);
        await service.RequestAsync("ana@example.test", Ct);
        PasswordResetConsumer consumer = f.ResetConsumer();

        await consumer.HandleAsync(f.Outbox.Messages[1], Ct);
        await consumer.HandleAsync(f.Outbox.Messages[0], Ct);

        Assert.Single(f.ResetTokens.Tokens);
        Assert.Single(f.Sender.Sent);
    }
}
