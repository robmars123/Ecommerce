using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Identity.Domain;

internal sealed class PasswordResetToken
{
    private PasswordResetToken() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset? InvalidatedAt { get; private set; }

    public static PasswordResetToken Create(Guid id, Guid userId, byte[] tokenHash, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (tokenHash is null || tokenHash.Length != RefreshToken.HashLength)
        {
            throw new DomainException("A reset token hash must be 32 bytes.");
        }
        if (expiresAt <= now)
        {
            throw new DomainException("A reset token must expire after it is created.");
        }
        return new PasswordResetToken { Id = id, UserId = userId, TokenHash = tokenHash, CreatedAt = now, ExpiresAt = expiresAt };
    }

    public bool CanRedeem(DateTimeOffset now) => UsedAt is null && InvalidatedAt is null && now < ExpiresAt;

    public void Redeem(DateTimeOffset now)
    {
        if (!CanRedeem(now))
        {
            throw new DomainException("This reset token cannot be redeemed.");
        }
        UsedAt = now;
    }

    public void Invalidate(DateTimeOffset now) => InvalidatedAt ??= now;
}
