using Ecommerce.Identity.Domain;

namespace Ecommerce.Identity.Application;

/// <summary>
/// The "ForUpdate" methods take a row lock (SELECT ... FOR UPDATE) and must run inside
/// <c>IUnitOfWork.InTransactionAsync</c>. The first load of an entity in a locking flow is the FOR UPDATE query.
/// </summary>
internal interface IUserRepository
{
    void Add(User user);

    Task<User?> FindByEmailForUpdateAsync(string email, CancellationToken ct);

    Task<User?> FindByIdForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>No tracking, no lock.</summary>
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>No tracking, no lock. Unknown ids are absent.</summary>
    Task<IReadOnlyList<User>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>No tracking, no lock.</summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);

    /// <summary>The id of the user with this normalised email, or null. No tracking, no lock (a read path, never the ForUpdate variant).</summary>
    Task<Guid?> FindIdByEmailAsync(string normalizedEmail, CancellationToken ct);

    /// <summary>Role Customer only, newest first. The search is a literal, case-insensitive substring of email or display name. No tracking, no lock.</summary>
    Task<(IReadOnlyList<User> Items, int TotalCount)> SearchCustomersAsync(string? search, int page, int pageSize, CancellationToken ct);

    /// <summary>The user if it has role Customer, else null. No tracking, no lock.</summary>
    Task<User?> FindCustomerAsync(Guid id, CancellationToken ct);
}

internal interface IRefreshTokenRepository
{
    void AddFamily(RefreshTokenFamily family);

    void AddToken(RefreshToken token);

    /// <summary>An untracked projection of the immutable family id; nothing is loaded into the change tracker.</summary>
    Task<Guid?> FindFamilyIdByTokenHashAsync(byte[] tokenHash, CancellationToken ct);

    Task<RefreshTokenFamily?> GetFamilyForUpdateAsync(Guid familyId, CancellationToken ct);

    /// <summary>Tracked. Call only after <see cref="GetFamilyForUpdateAsync"/> holds the family lock.</summary>
    Task<RefreshToken?> FindTokenByHashAsync(byte[] tokenHash, CancellationToken ct);

    /// <summary>Set-based; runs immediately inside the open transaction.</summary>
    Task RevokeAllFamiliesForUserAsync(Guid userId, RefreshTokenRevocationReason reason, DateTimeOffset now, CancellationToken ct);
}

internal interface IPasswordResetTokenRepository
{
    void Add(PasswordResetToken token);

    /// <summary>An untracked projection of the immutable user id.</summary>
    Task<Guid?> FindUserIdByTokenHashAsync(byte[] tokenHash, CancellationToken ct);

    Task<PasswordResetToken?> GetForUpdateByHashAsync(byte[] tokenHash, CancellationToken ct);

    /// <summary>Set-based; invalidates every outstanding (unused, not yet invalidated) token of the user.</summary>
    Task InvalidateOutstandingAsync(Guid userId, DateTimeOffset now, CancellationToken ct);

    /// <summary>True if the user has a token minted for a request newer than this one. Needs the user lock.</summary>
    Task<bool> HasNewerRequestAsync(Guid userId, DateTimeOffset requestedAt, CancellationToken ct);

    /// <summary>Set-based; invalidates the user's outstanding tokens from older requests (requested_at before this one, or null).</summary>
    Task InvalidateOlderAsync(Guid userId, DateTimeOffset requestedAt, DateTimeOffset now, CancellationToken ct);
}

internal enum PasswordVerification
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

internal interface IPasswordHashing
{
    string Hash(string password);

    PasswordVerification Verify(string hash, string password);

    /// <summary>Burns the same work as a real verification against a fixed hash, so "no account" and "locked" cost the same.</summary>
    void VerifyAgainstDummy(string password);
}

internal interface IAccessTokenIssuer
{
    (string Token, DateTimeOffset ExpiresAt) Issue(User user, DateTimeOffset now);
}

internal interface ISecureTokenGenerator
{
    /// <summary>256 random bits, base64url.</summary>
    string NewToken();

    string NewSecurityStamp();
}

internal interface ISecurityEventSink
{
    void RefreshTokenReuseDetected(Guid userId, Guid familyId);

    void AccountLockedOut(Guid userId);
}
