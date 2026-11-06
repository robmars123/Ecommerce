using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.Identity.Infrastructure.Jwt;

/// <summary>
/// D4. HS256, claims sub / role / jti, plus iss / aud. iat, nbf and exp come from the injected clock (never the system
/// clock), set explicitly on the descriptor.
/// </summary>
internal sealed class JwtAccessTokenIssuer(JwtSigningKeyProvider keys, IOptions<JwtOptions> options) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Issue(User user, DateTimeOffset now)
    {
        JwtOptions settings = options.Value;
        DateTimeOffset expiresAt = now.AddMinutes(settings.AccessTokenMinutes);
        SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                ["role"] = user.Role.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7(now).ToString(),
            },
            SigningCredentials = new SigningCredentials(keys.Key, SecurityAlgorithms.HmacSha256),
        };
        return (_handler.CreateToken(descriptor), expiresAt);
    }
}
