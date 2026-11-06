namespace Ecommerce.Identity.Contracts;

/// <summary>Policy names. The policies themselves are registered by the Identity module.</summary>
public static class AuthorizationPolicies
{
    public const string Admin = "Admin";
    public const string AdminOrSupport = "AdminOrSupport";
}
