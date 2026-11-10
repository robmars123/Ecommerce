namespace Ecommerce.Identity.Contracts;

public sealed record UserContact(string Email, string DisplayName);

public sealed record CustomerSummary(Guid Id, string Email, string DisplayName, DateTimeOffset CreatedAt);

public sealed record CustomerPage(IReadOnlyList<CustomerSummary> Items, int TotalCount);

/// <summary>Identity's read for other modules. Null (or an omitted id) when the user does not exist.</summary>
public interface IUserReader
{
    Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct);

    /// <summary>One entry per known id; unknown ids are omitted.</summary>
    Task<IReadOnlyDictionary<Guid, UserContact>> GetContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>The user id for an exact email (Identity's normalisation, as at login), or null if none. Read only, no lock.</summary>
    Task<Guid?> FindIdByEmailAsync(string email, CancellationToken ct);

    /// <summary>Users with role Customer only, newest first (created_at, then id). The search is a case-insensitive substring of email or display name, taken literally. Pages are 1-based.</summary>
    Task<CustomerPage> SearchCustomersAsync(string? search, int page, int pageSize, CancellationToken ct);

    /// <summary>Null when the user does not exist or is not a Customer.</summary>
    Task<CustomerSummary?> GetCustomerAsync(Guid userId, CancellationToken ct);
}
