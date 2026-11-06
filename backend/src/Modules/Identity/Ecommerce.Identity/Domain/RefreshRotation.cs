using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Identity.Domain;

/// <summary>
/// The refresh decision, pure. Order matters: revoked family, then reuse (a consumed token), then expiry, then rotate.
/// A consumed token is reuse even when it is also expired: replaying a dead token still proves two parties held it.
/// </summary>
internal static class RefreshRotation
{
    public static RotationOutcome Decide(RefreshTokenFamily family, RefreshToken token, DateTimeOffset now)
    {
        if (family.IsRevoked)
        {
            return RotationOutcome.FamilyRevoked;
        }
        if (token.ConsumedAt is not null)
        {
            return RotationOutcome.Reuse;
        }
        if (token.ExpiresAt <= now || family.AbsoluteExpiresAt <= now)
        {
            return RotationOutcome.Expired;
        }
        return RotationOutcome.Rotate;
    }

    /// <summary>Consumes <paramref name="presented"/> and returns its child. The child never outlives the family.</summary>
    public static RefreshToken Rotate(
        RefreshTokenFamily family, RefreshToken presented, Guid newId, byte[] newHash, DateTimeOffset now, TimeSpan sliding)
    {
        if (Decide(family, presented, now) != RotationOutcome.Rotate)
        {
            throw new DomainException("This refresh token cannot be rotated.");
        }
        DateTimeOffset expiresAt = now + sliding;
        if (expiresAt > family.AbsoluteExpiresAt)
        {
            expiresAt = family.AbsoluteExpiresAt;
        }
        presented.Consume(newId, now);
        return RefreshToken.Create(newId, family.Id, newHash, now, expiresAt);
    }
}
