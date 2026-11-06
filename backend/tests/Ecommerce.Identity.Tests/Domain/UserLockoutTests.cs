using Ecommerce.Identity.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Identity.Tests.Domain;

// ID-1: lockout arithmetic on the User aggregate.
public class UserLockoutTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly LockoutPolicy Policy = new(5, TimeSpan.FromMinutes(5));

    private static User NewUser() =>
        User.Register(Guid.NewGuid(), "ana@example.test", "Ana", "hash-1", "stamp-1", T0);

    [Fact]
    public void Four_failures_do_not_lock_and_the_fifth_locks_for_the_duration_and_resets_the_count()
    {
        User user = NewUser();

        for (int i = 1; i <= 4; i++)
        {
            Assert.False(user.RecordFailedLogin(T0, Policy));
            Assert.Equal(i, user.AccessFailedCount);
            Assert.False(user.IsLockedOut(T0));
        }

        Assert.True(user.RecordFailedLogin(T0, Policy));
        Assert.Equal(T0 + TimeSpan.FromMinutes(5), user.LockoutEnd);
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public void Lockout_is_active_until_exactly_the_end_instant()
    {
        User user = NewUser();
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(T0, Policy);
        }

        Assert.True(user.IsLockedOut(T0 + TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59)));
        Assert.False(user.IsLockedOut(T0 + TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void A_success_resets_the_failure_count_and_clears_the_lockout()
    {
        User user = NewUser();
        user.RecordFailedLogin(T0, Policy);
        user.RecordFailedLogin(T0, Policy);

        user.RecordSuccessfulLogin(T0);

        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public void Replacing_the_password_clears_the_lockout_and_changes_the_stamp()
    {
        User user = NewUser();
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(T0, Policy);
        }
        Assert.True(user.IsLockedOut(T0));

        user.ReplacePasswordHash("hash-2", "stamp-2", T0);

        Assert.False(user.IsLockedOut(T0));
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Equal("hash-2", user.PasswordHash);
        Assert.Equal("stamp-2", user.SecurityStamp);
    }

    [Fact]
    public void Upgrading_the_hash_keeps_the_stamp()
    {
        User user = NewUser();

        user.UpgradePasswordHash("hash-2", T0);

        Assert.Equal("hash-2", user.PasswordHash);
        Assert.Equal("stamp-1", user.SecurityStamp);
    }

    [Fact]
    public void Registration_always_creates_a_customer_and_bootstrap_creates_an_admin()
    {
        Assert.Equal(UserRole.Customer, NewUser().Role);
        Assert.Equal(UserRole.Admin, User.CreateAdmin(Guid.NewGuid(), "root@example.test", "Root", "h", "s", T0).Role);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void Lockout_policy_rejects_nonsense(int attempts, int minutes) =>
        Assert.Throws<DomainException>(() => new LockoutPolicy(attempts, TimeSpan.FromMinutes(minutes)));
}
