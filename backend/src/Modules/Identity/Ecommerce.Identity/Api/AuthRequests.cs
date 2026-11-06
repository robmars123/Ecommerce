namespace Ecommerce.Identity.Api;

// Request DTOs carry exactly these fields and no others. Unknown JSON members are a 400 (strict binding, D13), so
// "role":"Admin" on register can never be silently ignored or bound.
internal sealed record RegisterRequest(string Email, string Password, string DisplayName);

internal sealed record LoginRequest(string Email, string Password);

/// <summary>Body token for native clients. Web clients send nothing and rely on the httpOnly cookie.</summary>
internal sealed record RefreshRequest(string? RefreshToken);

internal sealed record LogoutRequest(string? RefreshToken);

internal sealed record PasswordResetRequest(string Email);

internal sealed record PasswordResetConfirmRequest(string Token, string NewPassword);
