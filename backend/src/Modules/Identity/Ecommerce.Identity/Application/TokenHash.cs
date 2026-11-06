using System.Security.Cryptography;
using System.Text;

namespace Ecommerce.Identity.Application;

/// <summary>
/// Opaque tokens are stored only as SHA-256. A fast hash is enough: the input has 256 bits of entropy,
/// so there is nothing to brute-force (unlike a password).
/// </summary>
internal static class TokenHash
{
    public static byte[] Of(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
