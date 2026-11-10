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

    public async Task<IReadOnlyDictionary<Guid, UserContact>> GetContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
        (await users.FindByIdsAsync(userIds, ct)).ToDictionary(u => u.Id, u => new UserContact(u.Email, u.DisplayName));

    public Task<Guid?> FindIdByEmailAsync(string email, CancellationToken ct) =>
        users.FindIdByEmailAsync(EmailAddress.Normalize(email), ct);

    public async Task<CustomerPage> SearchCustomersAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        (IReadOnlyList<User> items, int total) = await users.SearchCustomersAsync(search, page, pageSize, ct);
        return new CustomerPage(items.Select(ToSummary).ToList(), total);
    }

    public async Task<CustomerSummary?> GetCustomerAsync(Guid userId, CancellationToken ct)
    {
        User? user = await users.FindCustomerAsync(userId, ct);
        return user is null ? null : ToSummary(user);
    }

    private static CustomerSummary ToSummary(User u) => new(u.Id, u.Email, u.DisplayName, u.CreatedAt);
}
