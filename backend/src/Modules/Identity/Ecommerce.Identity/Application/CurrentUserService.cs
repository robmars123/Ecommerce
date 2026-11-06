using Ecommerce.Identity.Domain;

namespace Ecommerce.Identity.Application;

internal sealed class CurrentUserService(IUserRepository users)
{
    public async Task<UserView?> GetAsync(Guid id, CancellationToken ct)
    {
        User? user = await users.FindByIdAsync(id, ct);
        return user is null ? null : UserView.From(user);
    }
}
