using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Identity.Domain;

internal sealed class RefreshTokenFamily
{
    private RefreshTokenFamily() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset AbsoluteExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public RefreshTokenRevocationReason? RevokedReason { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public static RefreshTokenFamily Create(Guid id, Guid userId, DateTimeOffset now, DateTimeOffset absoluteExpiresAt)
    {
        if (absoluteExpiresAt <= now)
        {
            throw new DomainException("A refresh token family must expire after it is created.");
        }
        return new RefreshTokenFamily { Id = id, UserId = userId, CreatedAt = now, AbsoluteExpiresAt = absoluteExpiresAt };
    }

    /// <summary>Idempotent: the first reason and time are kept.</summary>
    public void Revoke(RefreshTokenRevocationReason reason, DateTimeOffset now)
    {
        if (IsRevoked)
        {
            return;
        }
        RevokedAt = now;
        RevokedReason = reason;
    }
}
