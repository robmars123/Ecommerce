namespace Ecommerce.Identity.Infrastructure.PasswordReset;

internal sealed class PasswordResetOptions
{
    public int TokenLifetimeMinutes { get; set; } = 30;

    /// <summary>Empty in committed config (no link). Development sets the local Expo web origin.</summary>
    public string LinkBaseUrl { get; set; } = "";

    /// <summary>Development only: a directory the dev sender writes link files into. Startup fails if set elsewhere.</summary>
    public string DevPickupDirectory { get; set; } = "";
}
