using Ecommerce.Identity.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Identity.Tests.Domain;

// ID-4: single use, newest-only, expiring.
public class PasswordResetTokenTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private static PasswordResetToken Fresh() =>
        PasswordResetToken.Create(Guid.NewGuid(), Guid.NewGuid(), new byte[32], T0, T0 + Lifetime);

    [Fact]
    public void A_fresh_token_can_be_redeemed_until_the_expiry_instant()
    {
        PasswordResetToken token = Fresh();

        Assert.True(token.CanRedeem(T0));
        Assert.True(token.CanRedeem(T0 + Lifetime - TimeSpan.FromSeconds(1)));
        Assert.False(token.CanRedeem(T0 + Lifetime));
        Assert.False(token.CanRedeem(T0 + Lifetime + TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Redeeming_marks_it_used_and_a_second_redeem_throws()
    {
        PasswordResetToken token = Fresh();

        token.Redeem(T0.AddMinutes(1));

        Assert.Equal(T0.AddMinutes(1), token.UsedAt);
        Assert.False(token.CanRedeem(T0.AddMinutes(2)));
        Assert.Throws<DomainException>(() => token.Redeem(T0.AddMinutes(2)));
    }

    [Fact]
    public void An_invalidated_token_cannot_be_redeemed()
    {
        PasswordResetToken token = Fresh();

        token.Invalidate(T0.AddMinutes(1));

        Assert.False(token.CanRedeem(T0.AddMinutes(2)));
        Assert.Throws<DomainException>(() => token.Redeem(T0.AddMinutes(2)));
    }

    [Fact]
    public void An_expired_token_cannot_be_redeemed()
    {
        PasswordResetToken token = Fresh();

        Assert.Throws<DomainException>(() => token.Redeem(T0 + Lifetime));
        Assert.Null(token.UsedAt);
    }

    [Fact]
    public void Invalidating_twice_keeps_the_first_time()
    {
        PasswordResetToken token = Fresh();
        token.Invalidate(T0.AddMinutes(1));

        token.Invalidate(T0.AddMinutes(5));

        Assert.Equal(T0.AddMinutes(1), token.InvalidatedAt);
    }
}
