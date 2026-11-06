using Ecommerce.Identity.Domain;

namespace Ecommerce.Identity.Application;

/// <summary>Starts a refresh-token family with its first token. Shared by register and login; runs inside their transaction.</summary>
internal sealed class SessionStarter(IRefreshTokenRepository tokens, ISecureTokenGenerator generator, TokenLifetimes lifetimes)
{
    public (string RefreshToken, DateTimeOffset ExpiresAt) StartFamily(Guid userId, DateTimeOffset now)
    {
        RefreshTokenFamily family = RefreshTokenFamily.Create(Guid.CreateVersion7(now), userId, now, now + lifetimes.RefreshAbsolute);
        string plaintext = generator.NewToken();
        DateTimeOffset expiresAt = now + lifetimes.RefreshSliding;
        if (expiresAt > family.AbsoluteExpiresAt)
        {
            expiresAt = family.AbsoluteExpiresAt;
        }
        RefreshToken token = RefreshToken.Create(Guid.CreateVersion7(now), family.Id, TokenHash.Of(plaintext), now, expiresAt);
        tokens.AddFamily(family);
        tokens.AddToken(token);
        // Only the SHA-256 is stored; the plaintext leaves this method once, in the response.
        return (plaintext, token.ExpiresAt);
    }
}
