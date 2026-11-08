using Ecommerce.Identity.Contracts;
using Ecommerce.Identity.Domain;

namespace Ecommerce.Identity.Application;

/// <summary>Identity's read for Notifications (D7): a user's email and display name, nothing else.</summary>
internal sealed class UserReader(IUserRepository users) : IUserReader
{
    public async Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct)
    {
        User? user = await users.FindByIdAsync(userId, ct);
        return user is null ? null : new UserContact(user.Email, user.DisplayName);
    }
}
