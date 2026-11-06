using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;
using Ecommerce.SharedKernel.Persistence;

namespace Ecommerce.Identity.Tests.Application;

// ID-6
public class RegistrationServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registration_always_creates_a_customer_with_a_session()
    {
        IdentityFakes f = new IdentityFakes();

        RegisterOutcome outcome = await f.Registration().RegisterAsync(" ana@example.test ", "a long password", "  Ana  ", Ct);

        SessionIssued session = Assert.IsType<RegisterOutcome.Created>(outcome).Session;
        User user = Assert.Single(f.Users.Users);
        Assert.Equal(UserRole.Customer, user.Role);
        Assert.Equal("ana@example.test", user.Email);
        Assert.Equal("Ana", user.DisplayName);
        Assert.Equal("hash:a long password", user.PasswordHash);
        Assert.Equal(UserRole.Customer, session.User.Role);
        Assert.Equal(TokenHash.Of(session.RefreshToken), Assert.Single(f.RefreshTokens.Tokens).TokenHash);
    }

    [Fact]
    public async Task Hashing_happens_before_the_transaction_opens()
    {
        IdentityFakes f = new IdentityFakes();

        await f.Registration().RegisterAsync("ana@example.test", "a long password", "Ana", Ct);

        Assert.True(f.Log.IndexOf("hash") >= 0 && f.Log.IndexOf("hash") < f.Log.IndexOf("tx-begin"), string.Join(",", f.Log));
    }

    [Fact]
    public async Task A_unique_violation_on_the_email_index_becomes_DuplicateEmail_and_issues_no_token()
    {
        IdentityFakes f = new IdentityFakes();
        f.UnitOfWork.FailOnCommit = new UniqueConstraintViolationException("ux_users_email");

        RegisterOutcome outcome = await f.Registration().RegisterAsync("ana@example.test", "a long password", "Ana", Ct);

        Assert.IsType<RegisterOutcome.DuplicateEmail>(outcome);
        Assert.DoesNotContain("issue-access-token", f.Log);
    }

    [Fact]
    public async Task Any_other_unique_violation_is_not_swallowed()
    {
        IdentityFakes f = new IdentityFakes();
        f.UnitOfWork.FailOnCommit = new UniqueConstraintViolationException("ux_refresh_tokens_token_hash");

        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() =>
            f.Registration().RegisterAsync("ana@example.test", "a long password", "Ana", Ct));
    }
}
