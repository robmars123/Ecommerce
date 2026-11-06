namespace Ecommerce.Identity.Domain;

internal enum RefreshTokenRevocationReason
{
    Logout,
    Reuse,
    PasswordReset,
}
