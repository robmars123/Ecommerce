using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.Identity.Infrastructure.Jwt;

/// <summary>
/// Holds the one signing key for the process. In Development an empty key means one ephemeral random key per process
/// (so nothing secret is committed and no setup step is needed). Any other environment fails at startup instead
/// (see <see cref="JwtOptionsValidation"/>); this class never invents a key there.
/// </summary>
internal sealed class JwtSigningKeyProvider
{
    public JwtSigningKeyProvider(IOptions<JwtOptions> options, IHostEnvironment environment, ILogger<JwtSigningKeyProvider> logger)
    {
        if (JwtOptions.TryDecodeKey(options.Value.SigningKey, out byte[]? bytes))
        {
            Key = new SymmetricSecurityKey(bytes);
            return;
        }
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(JwtOptionsValidation.KeyMessage);
        }

        logger.LogWarning(
            "Identity:Jwt:SigningKey is empty; using an ephemeral key for this process. Tokens stop validating when the API restarts.");
        Key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(JwtOptions.MinKeyBytes));
    }

    public SymmetricSecurityKey Key { get; }
}

internal static class JwtOptionsValidation
{
    public const string KeyMessage =
        "Identity:Jwt:SigningKey must be base64 of at least 32 bytes. Set it with dotnet user-secrets or an environment variable.";

    public static bool IsValid(JwtOptions o, IHostEnvironment environment)
    {
        bool keyOk = JwtOptions.TryDecodeKey(o.SigningKey, out _)
                    || (string.IsNullOrWhiteSpace(o.SigningKey) && environment.IsDevelopment());
        return keyOk
               && !string.IsNullOrWhiteSpace(o.Issuer)
               && !string.IsNullOrWhiteSpace(o.Audience)
               && o.AccessTokenMinutes is >= 1 and <= 60;
    }
}
