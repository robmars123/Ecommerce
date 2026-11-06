using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;

namespace Ecommerce.Identity.Tests.Application;

// ID-8 (the concurrency behavior itself is proven on real Postgres: II-4, II-5).
public class RefreshServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (IdentityFakes F, User User, string Token) SignedIn()
    {
        IdentityFakes f = new IdentityFakes();
        User user = f.AddUser();
        (string? token, DateTimeOffset _) = f.Sessions.StartFamily(user.Id, IdentityFakes.T0);
        return (f, user, token);
    }

    [Fact]
    public async Task A_valid_token_rotates_and_the_old_one_is_consumed()
    {
        (IdentityFakes? f, User? user, string? token) = SignedIn();
        f.Clock.UtcNow = IdentityFakes.T0.AddDays(1);

        RefreshOutcome outcome = await f.Refresh().RefreshAsync(token, Ct);

        SessionIssued session = Assert.IsType<RefreshOutcome.Success>(outcome).Session;
        Assert.NotEqual(token, session.RefreshToken);
        Assert.Equal(user.Id, session.User.Id);
        Assert.Equal(2, f.RefreshTokens.Tokens.Count);
        Assert.NotNull(f.RefreshTokens.Tokens[0].ConsumedAt);
        Assert.Equal(TokenHash.Of(session.RefreshToken), f.RefreshTokens.Tokens[1].TokenHash);
        Assert.Empty(f.Security.Reuses);
    }

    [Fact]
    public async Task Replaying_a_consumed_token_revokes_the_family_commits_and_notifies_the_sink()
    {
        (IdentityFakes? f, User? user, string? token) = SignedIn();
        RefreshService service = f.Refresh();
        f.Clock.UtcNow = IdentityFakes.T0.AddDays(1);
        Assert.IsType<RefreshOutcome.Success>(await service.RefreshAsync(token, Ct));
        f.Log.Clear();

        RefreshOutcome outcome = await service.RefreshAsync(token, Ct);

        Assert.IsType<RefreshOutcome.Rejected>(outcome);
        RefreshTokenFamily family = Assert.Single(f.RefreshTokens.Families);
        Assert.Equal(RefreshTokenRevocationReason.Reuse, family.RevokedReason);
        Assert.Equal((user.Id, family.Id), Assert.Single(f.Security.Reuses));
        Assert.Contains("tx-commit", f.Log);
        Assert.DoesNotContain("tx-rollback", f.Log);
    }

    [Fact]
    public async Task After_reuse_the_newest_child_token_is_dead_too()
    {
        (IdentityFakes? f, User _, string? token) = SignedIn();
        RefreshService service = f.Refresh();
        f.Clock.UtcNow = IdentityFakes.T0.AddDays(1);
        string child = Assert.IsType<RefreshOutcome.Success>(await service.RefreshAsync(token, Ct)).Session.RefreshToken;
        await service.RefreshAsync(token, Ct);

        Assert.IsType<RefreshOutcome.Rejected>(await service.RefreshAsync(child, Ct));
    }

    [Fact]
    public async Task A_revoked_family_is_rejected_without_a_new_revocation_or_security_event()
    {
        (IdentityFakes? f, User _, string? token) = SignedIn();
        RefreshTokenFamily family = Assert.Single(f.RefreshTokens.Families);
        family.Revoke(RefreshTokenRevocationReason.Logout, IdentityFakes.T0);

        RefreshOutcome outcome = await f.Refresh().RefreshAsync(token, Ct);

        Assert.IsType<RefreshOutcome.Rejected>(outcome);
        Assert.Equal(RefreshTokenRevocationReason.Logout, family.RevokedReason);
        Assert.Empty(f.Security.Reuses);
    }

    [Fact]
    public async Task An_expired_token_is_rejected_and_the_family_stays_unrevoked()
    {
        (IdentityFakes? f, User _, string? token) = SignedIn();
        f.Clock.UtcNow = IdentityFakes.T0 + IdentityFakes.Lifetimes.RefreshSliding;

        RefreshOutcome outcome = await f.Refresh().RefreshAsync(token, Ct);

        Assert.IsType<RefreshOutcome.Rejected>(outcome);
        Assert.False(Assert.Single(f.RefreshTokens.Families).IsRevoked);
        Assert.Empty(f.Security.Reuses);
    }

    [Fact]
    public async Task An_unknown_token_is_rejected_without_a_transaction()
    {
        (IdentityFakes? f, User _, string _) = SignedIn();

        RefreshOutcome outcome = await f.Refresh().RefreshAsync("garbage", Ct);

        Assert.IsType<RefreshOutcome.Rejected>(outcome);
        Assert.Equal(0, f.UnitOfWork.Transactions);
    }

    [Fact]
    public async Task Refresh_reads_the_role_again_so_a_role_change_applies_to_the_new_access_token()
    {
        (IdentityFakes? f, User? user, string? token) = SignedIn();
        f.Users.Users.Remove(user);
        User promoted = User.CreateAdmin(user.Id, user.Email, "Ana", user.PasswordHash, "stamp-0", IdentityFakes.T0);
        f.Users.Users.Add(promoted);

        RefreshOutcome outcome = await f.Refresh().RefreshAsync(token, Ct);

        Assert.Equal(UserRole.Admin, Assert.IsType<RefreshOutcome.Success>(outcome).Session.User.Role);
    }

    [Fact]
    public async Task Logout_revokes_the_family_and_is_silent_for_garbage()
    {
        (IdentityFakes? f, User _, string? token) = SignedIn();
        RefreshService service = f.Refresh();

        await service.LogoutAsync(token, Ct);
        await service.LogoutAsync(token, Ct);
        await service.LogoutAsync("garbage", Ct);

        Assert.Equal(RefreshTokenRevocationReason.Logout, Assert.Single(f.RefreshTokens.Families).RevokedReason);
        Assert.IsType<RefreshOutcome.Rejected>(await service.RefreshAsync(token, Ct));
    }
}
