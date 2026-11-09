namespace Ecommerce.Identity.Contracts;

public sealed record UserContact(string Email, string DisplayName);

/// <summary>Identity's read for other modules. Null (or an omitted id) when the user does not exist.</summary>
public interface IUserReader
{
    Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct);

    /// <summary>One entry per known id; unknown ids are omitted.</summary>
    Task<IReadOnlyDictionary<Guid, UserContact>> GetContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>The user id for an exact email (Identity's normalisation, as at login), or null if none. Read only, no lock.</summary>
    Task<Guid?> FindIdByEmailAsync(string email, CancellationToken ct);
}
