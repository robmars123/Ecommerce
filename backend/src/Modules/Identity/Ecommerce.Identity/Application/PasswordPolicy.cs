namespace Ecommerce.Identity.Application;

/// <summary>NIST SP 800-63B: length, not composition. The maximum bounds PBKDF2 input.</summary>
internal static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;
}
