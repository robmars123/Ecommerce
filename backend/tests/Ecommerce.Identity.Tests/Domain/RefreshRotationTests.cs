using Ecommerce.Identity.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Identity.Tests.Domain;

// ID-2: the rotation decision table and the child-expiry arithmetic.
public class RefreshRotationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Sliding = TimeSpan.FromDays(14);
    private static readonly TimeSpan Absolute = TimeSpan.FromDays(30);

    private static (RefreshTokenFamily Family, RefreshToken Token) Fresh()
    {
        RefreshTokenFamily family = RefreshTokenFamily.Create(Guid.NewGuid(), Guid.NewGuid(), T0, T0 + Absolute);
        RefreshToken token = RefreshToken.Create(Guid.NewGuid(), family.Id, new byte[32], T0, T0 + Sliding);
        return (family, token);
    }

    [Fact]
    public void A_valid_token_rotates()
    {
        (RefreshTokenFamily? family, RefreshToken? token) = Fresh();

        Assert.Equal(RotationOutcome.Rotate, RefreshRotation.Decide(family, token, T0.AddDays(1)));
    }

    [Fact]
    public void A_revoked_family_wins_even_over_a_fresh_token()
    {
        (RefreshTokenFamily? family, RefreshToken? token) = Fresh();
        family.Revoke(RefreshTokenRevocationReason.Logout, T0.AddMinutes(1));

        Assert.Equal(RotationOutcome.FamilyRevoked, RefreshRotation.Decide(family, token, T0.AddMinutes(2)));
    }

    [Fact]
    public void A_consumed_token_is_reuse_and_revoking_the_family_records_reason_reuse()
    {
        (RefreshTokenFamily? family, RefreshToken? token) = Fresh();
        RefreshRotation.Rotate(family, token, Guid.NewGuid(), new byte[32], T0.AddDays(1), Sliding);

        Assert.Equal(RotationOutcome.Reuse, RefreshRotation.Decide(family, token, T0.AddDays(2)));

        family.Revoke(RefreshTokenRevocationReason.Reuse, T0.AddDays(2));
        Assert.Equal(RefreshTokenRevocationReason.Reuse, family.RevokedReason);
        Assert.Equal(T0.AddDays(2), family.RevokedAt);
    }

    [Fact]
    public void A_consumed_and_expired_token_is_still_reuse()
    {
        (RefreshTokenFamily? family, RefreshToken? token) = Fresh();
        RefreshRotation.Rotate(family, token, Guid.NewGuid(), new byte[32], T0.AddDays(1), Sliding);

        Assert.Equal(RotationOutcome.Reuse, RefreshRotation.Decide(family, token, T0 + Sliding + TimeSpan.FromDays(1)));
    }

    [Fact]
    public void A_second_revoke_keeps_the_first_reason_and_time()
    {
        (RefreshTokenFamily? family, RefreshToken _) = Fresh();
        family.Revoke(RefreshTokenRevocationReason.Reuse, T0.AddDays(1));

        family.Revoke(RefreshTokenRevocationReason.Logout, T0.AddDays(2));

        Assert.Equal(RefreshTokenRevocationReason.Reuse, family.RevokedReason);
        Assert.Equal(T0.AddDays(1), family.RevokedAt);
    }

    [Fact]
    public void An_expired_token_is_expired_not_reuse()
    {
        (RefreshTokenFamily? family, RefreshToken? token) = Fresh();

        Assert.Equal(RotationOutcome.Expired, RefreshRotation.Decide(family, token, T0 + Sliding));
        Assert.Equal(RotationOutcome.Rotate, RefreshRotation.Decide(family, token, T0 + Sliding - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void A_family_past_its_absolute_expiry_is_expired_even_with_a_live_token()
    {
        RefreshTokenFamily family = RefreshTokenFamily.Create(Guid.NewGuid(), Guid.NewGuid(), T0, T0 + Absolute);
        RefreshToken token = RefreshToken.Create(
            Guid.NewGuid(), family.Id, new byte[32], T0 + Absolute - TimeSpan.FromDays(1), T0 + Absolute + TimeSpan.FromDays(5));

        Assert.Equal(RotationOutcome.Expired, RefreshRotation.Decide(family, token, T0 + Absolute));
    }

    [Fact]
    public void Rotating_consumes_the_presented_token_links_the_child_and_slides_the_expiry()
    {
        (RefreshTokenFamily? family, RefreshToken? token) = Fresh();
        Guid newId = Guid.NewGuid();
        byte[] newHash = Enumerable.Repeat((byte)7, 32).ToArray();
        DateTimeOffset now = T0.AddDays(1);

        RefreshToken child = RefreshRotation.Rotate(family, token, newId, newHash, now, Sliding);

        Assert.Equal(now, token.ConsumedAt);
        Assert.Equal(newId, token.ReplacedById);
        Assert.Equal(newId, child.Id);
        Assert.Equal(family.Id, child.FamilyId);
        Assert.Equal(newHash, child.TokenHash);
        Assert.Equal(now + Sliding, child.ExpiresAt);
        Assert.Null(child.ConsumedAt);
    }

    [Fact]
    public void Child_expiry_is_capped_by_the_family_absolute_expiry_at_both_edges()
    {
        // Sliding end is one second before the absolute end: sliding wins.
        (RefreshTokenFamily? family, RefreshToken? token) = Fresh();
        DateTimeOffset justInside = T0 + Absolute - Sliding - TimeSpan.FromSeconds(1);
        RefreshToken tokenInside = RefreshToken.Create(Guid.NewGuid(), family.Id, new byte[32], T0, justInside + TimeSpan.FromDays(1));
        Assert.Equal(
            justInside + Sliding,
            RefreshRotation.Rotate(family, tokenInside, Guid.NewGuid(), new byte[32], justInside, Sliding).ExpiresAt);

        // Sliding end would be one second past the absolute end: the cap wins.
        DateTimeOffset justOutside = T0 + Absolute - Sliding + TimeSpan.FromSeconds(1);
        RefreshToken tokenOutside = RefreshToken.Create(Guid.NewGuid(), family.Id, new byte[32], T0, justOutside + TimeSpan.FromDays(1));
        Assert.Equal(
            family.AbsoluteExpiresAt,
            RefreshRotation.Rotate(family, tokenOutside, Guid.NewGuid(), new byte[32], justOutside, Sliding).ExpiresAt);
        Assert.NotNull(token);
    }

    [Fact]
    public void Rotating_a_token_that_cannot_rotate_throws()
    {
        (RefreshTokenFamily? family, RefreshToken? token) = Fresh();
        RefreshRotation.Rotate(family, token, Guid.NewGuid(), new byte[32], T0.AddDays(1), Sliding);

        Assert.Throws<DomainException>(() =>
            RefreshRotation.Rotate(family, token, Guid.NewGuid(), new byte[32], T0.AddDays(2), Sliding));
    }
}
