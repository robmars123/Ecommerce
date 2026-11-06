namespace Ecommerce.Identity.Infrastructure.Jwt;

internal sealed class JwtOptions
{
    public const int MinKeyBytes = 32;

    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";

    /// <summary>Base64 of at least 32 random bytes. Empty in committed config; set by user-secrets or an environment variable.</summary>
    public string SigningKey { get; set; } = "";

    public int AccessTokenMinutes { get; set; } = 15;

    public static bool TryDecodeKey(string? base64, out byte[] key)
    {
        key = [];
        if (string.IsNullOrWhiteSpace(base64))
        {
            return false;
        }
        try
        {
            key = Convert.FromBase64String(base64);
            return key.Length >= MinKeyBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
