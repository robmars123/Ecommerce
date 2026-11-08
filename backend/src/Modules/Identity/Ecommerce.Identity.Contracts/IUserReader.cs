namespace Ecommerce.Identity.Contracts;

public sealed record UserContact(string Email, string DisplayName);

/// <summary>Identity's read for Notifications (one consumer). Null when the user does not exist.</summary>
public interface IUserReader
{
    Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct);
}
