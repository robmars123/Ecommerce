using System.Security.Cryptography;
using Ecommerce.Identity.Application;
using Microsoft.AspNetCore.Identity;

namespace Ecommerce.Identity.Infrastructure;

/// <summary>
/// ASP.NET Core Identity's PasswordHasher (v3 format: PBKDF2-HMAC-SHA512, random salt, versioned, rehash-on-upgrade).
/// Only the tested crypto is borrowed; users, lockout and tokens are this module's own (D1).
/// </summary>
internal sealed class IdentityPasswordHashing : IPasswordHashing
{
    private readonly PasswordHasher<object> _hasher = new();

    // Computed once, from a random value, never a committed literal. Verifying against it costs the same as a real hash.
    private readonly Lazy<string> _dummyHash;

    public IdentityPasswordHashing()
    {
        _dummyHash = new Lazy<string>(() =>
            _hasher.HashPassword(null!, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
    }

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public PasswordVerification Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(null!, hash, password) switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed,
        };

    public void VerifyAgainstDummy(string password) => _hasher.VerifyHashedPassword(null!, _dummyHash.Value, password);
}
