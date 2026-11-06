namespace Ecommerce.Identity.Application;

internal sealed record TokenLifetimes(TimeSpan AccessToken, TimeSpan RefreshSliding, TimeSpan RefreshAbsolute, TimeSpan PasswordReset);

internal sealed record RefreshCookieSettings(string Name, bool Secure);

/// <summary>LinkBaseUrl is empty in committed config; with no base URL no link is built.</summary>
internal sealed record PasswordResetLinkSettings(string? LinkBaseUrl);
