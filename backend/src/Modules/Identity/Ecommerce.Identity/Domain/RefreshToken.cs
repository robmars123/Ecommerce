using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Identity.Domain;

internal sealed class RefreshToken
{
    public const int HashLength = 32;

    private RefreshToken() { }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public Guid? ReplacedById { get; private set; }

    public static RefreshToken Create(Guid id, Guid familyId, byte[] tokenHash, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (tokenHash is null || tokenHash.Length != HashLength)
        {
            throw new DomainException($"A refresh token hash must be {HashLength} bytes.");
        }
        if (expiresAt <= now)
        {
            throw new DomainException("A refresh token must expire after it is created.");
        }
        return new RefreshToken { Id = id, FamilyId = familyId, TokenHash = tokenHash, CreatedAt = now, ExpiresAt = expiresAt };
    }

    internal void Consume(Guid replacedById, DateTimeOffset now)
    {
        if (ConsumedAt is not null)
        {
            throw new DomainException("This refresh token was already consumed.");
        }
        ConsumedAt = now;
        ReplacedById = replacedById;
    }
}
