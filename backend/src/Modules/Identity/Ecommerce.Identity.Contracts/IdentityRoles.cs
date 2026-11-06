namespace Ecommerce.Identity.Contracts;

/// <summary>Role names carried in the JWT <c>role</c> claim and stored in <c>users.role</c>.</summary>
public static class IdentityRoles
{
    public const string Customer = "Customer";
    public const string Support = "Support";
    public const string Admin = "Admin";
}
