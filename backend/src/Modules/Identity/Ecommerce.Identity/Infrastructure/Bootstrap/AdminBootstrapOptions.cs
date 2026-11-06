namespace Ecommerce.Identity.Infrastructure.Bootstrap;

/// <summary>Both empty in committed config. Set through user-secrets or environment variables only.</summary>
internal sealed class AdminBootstrapOptions
{
    public string AdminEmail { get; set; } = "";

    public string AdminPassword { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AdminEmail) && !string.IsNullOrEmpty(AdminPassword);
}
