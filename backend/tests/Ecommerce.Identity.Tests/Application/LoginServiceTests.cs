using System.Text;
using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;

namespace Ecommerce.Identity.Tests.Application;

// ID-5: the login flow, in the exact order D7 requires.
public class LoginServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unknown_email_runs_exactly_one_dummy_verification_and_is_rejected()
    {
        IdentityFakes f = new IdentityFakes();

        LoginOutcome outcome = await f.Login().LoginAsync("nobody@example.test", "whatever-123", Ct);

        Assert.IsType<LoginOutcome.InvalidCredentials>(outcome);
        Assert.Equal(1, f.Hashing.DummyVerifications);
        Assert.Equal(0, f.Hashing.RealVerifications);
        Assert.Empty(f.RefreshTokens.Families);
    }

    [Fact]
    public async Task A_locked_account_runs_a_dummy_verification_and_leaves_the_failure_count_unchanged()
    {
        IdentityFakes f = new IdentityFakes();
        User user = f.AddUser();
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(IdentityFakes.T0, IdentityFakes.Lockout);
        }
        DateTimeOffset? lockoutEnd = user.LockoutEnd;

        // Even the CORRECT password must not get through while locked.
        LoginOutcome outcome = await f.Login().LoginAsync("ana@example.test", "correct horse", Ct);

        Assert.IsType<LoginOutcome.InvalidCredentials>(outcome);
        Assert.Equal(1, f.Hashing.DummyVerifications);
        Assert.Equal(0, f.Hashing.RealVerifications);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Equal(lockoutEnd, user.LockoutEnd);
        Assert.Empty(f.RefreshTokens.Families);
    }

    [Fact]
    public async Task A_wrong_password_counts_a_failure_and_the_locking_attempt_notifies_the_sink_once()
    {
        IdentityFakes f = new IdentityFakes();
        User user = f.AddUser();
        LoginService login = f.Login();

        for (int i = 1; i <= 4; i++)
        {
            Assert.IsType<LoginOutcome.InvalidCredentials>(await login.LoginAsync("ana@example.test", "wrong-password", Ct));
            Assert.Equal(i, user.AccessFailedCount);
            Assert.Empty(f.Security.Lockouts);
        }

        Assert.IsType<LoginOutcome.InvalidCredentials>(await login.LoginAsync("ana@example.test", "wrong-password", Ct));

        Assert.Equal(user.Id, Assert.Single(f.Security.Lockouts));
        Assert.True(user.IsLockedOut(IdentityFakes.T0));
        Assert.Equal(5, f.Hashing.RealVerifications);
    }

    [Fact]
    public async Task Success_creates_a_family_and_stores_only_the_hash_of_the_refresh_token()
    {
        IdentityFakes f = new IdentityFakes();
        User user = f.AddUser();

        LoginOutcome outcome = await f.Login().LoginAsync("  ana@example.test ", "correct horse", Ct);

        SessionIssued session = Assert.IsType<LoginOutcome.Success>(outcome).Session;
        RefreshTokenFamily family = Assert.Single(f.RefreshTokens.Families);
        RefreshToken stored = Assert.Single(f.RefreshTokens.Tokens);
        Assert.Equal(user.Id, family.UserId);
        Assert.Equal(family.Id, stored.FamilyId);
        Assert.Equal(TokenHash.Of(session.RefreshToken), stored.TokenHash);
        Assert.NotEqual(Encoding.UTF8.GetBytes(session.RefreshToken), stored.TokenHash);
        Assert.Equal(IdentityFakes.T0 + IdentityFakes.Lifetimes.RefreshSliding, session.RefreshTokenExpiresAt);
        Assert.Equal(IdentityFakes.T0 + IdentityFakes.Lifetimes.RefreshAbsolute, family.AbsoluteExpiresAt);
        Assert.Equal(user.Id, session.User.Id);
    }

    [Fact]
    public async Task Success_resets_earlier_failures()
    {
        IdentityFakes f = new IdentityFakes();
        User user = f.AddUser();
        await f.Login().LoginAsync("ana@example.test", "wrong-password", Ct);
        Assert.Equal(1, user.AccessFailedCount);

        await f.Login().LoginAsync("ana@example.test", "correct horse", Ct);

        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task A_rehash_verdict_upgrades_the_stored_hash_without_changing_the_stamp()
    {
        IdentityFakes f = new IdentityFakes { };
        f.Hashing.RehashNeeded = true;
        User user = f.AddUser();
        user.UpgradePasswordHash("hash:correct horse", IdentityFakes.T0);
        string stampBefore = user.SecurityStamp;

        await f.Login().LoginAsync("ana@example.test", "correct horse", Ct);

        Assert.Contains("hash", f.Log);
        Assert.Equal(stampBefore, user.SecurityStamp);
    }

    [Fact]
    public async Task No_rehash_when_the_hash_is_current()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();

        await f.Login().LoginAsync("ana@example.test", "correct horse", Ct);

        Assert.DoesNotContain("hash", f.Log);
    }

    [Fact]
    public async Task The_access_token_is_issued_only_after_the_transaction_committed()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();

        await f.Login().LoginAsync("ana@example.test", "correct horse", Ct);

        int commit = f.Log.IndexOf("tx-commit");
        int issue = f.Log.IndexOf("issue-access-token");
        Assert.True(commit >= 0 && issue > commit, "access token must be issued after tx-commit: " + string.Join(",", f.Log));
    }

    [Fact]
    public async Task Failed_attempts_still_commit_so_the_counter_is_durable()
    {
        IdentityFakes f = new IdentityFakes();
        f.AddUser();

        await f.Login().LoginAsync("ana@example.test", "wrong-password", Ct);

        Assert.Contains("tx-commit", f.Log);
        Assert.DoesNotContain("tx-rollback", f.Log);
        Assert.DoesNotContain("issue-access-token", f.Log);
    }
}
