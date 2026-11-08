using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Identity.Infrastructure;

internal static class LockGuard
{
    /// <summary>A FOR UPDATE outside a transaction would lock and release in the same statement: a silent no-op.</summary>
    public static void RequireTransaction(EcommerceDbContext db, string method)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException($"{method} takes a row lock and must run inside IUnitOfWork.InTransactionAsync.");
        }
    }
}

internal sealed class UserRepository(EcommerceDbContext db) : IUserRepository
{
    public void Add(User user) => db.Set<User>().Add(user);

    public async Task<User?> FindByEmailForUpdateAsync(string email, CancellationToken ct)
    {
        LockGuard.RequireTransaction(db, nameof(FindByEmailForUpdateAsync));
        // The explicit cast keeps the comparison citext = citext (case-insensitive). A bare text parameter would
        // silently fall back to case-sensitive text = text.
        List<User> rows = await db.Set<User>()
            .FromSql($"SELECT * FROM users WHERE email = CAST({email} AS citext) FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public async Task<User?> FindByIdForUpdateAsync(Guid id, CancellationToken ct)
    {
        LockGuard.RequireTransaction(db, nameof(FindByIdForUpdateAsync));
        List<User> rows = await db.Set<User>()
            .FromSql($"SELECT * FROM users WHERE id = {id} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct) =>
        db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
}

internal sealed class RefreshTokenRepository(EcommerceDbContext db) : IRefreshTokenRepository
{
    public void AddFamily(RefreshTokenFamily family) => db.Set<RefreshTokenFamily>().Add(family);

    public void AddToken(RefreshToken token) => db.Set<RefreshToken>().Add(token);

    public Task<Guid?> FindFamilyIdByTokenHashAsync(byte[] tokenHash, CancellationToken ct) =>
        db.Set<RefreshToken>().AsNoTracking()
            .Where(t => t.TokenHash == tokenHash)
            .Select(t => (Guid?)t.FamilyId)
            .FirstOrDefaultAsync(ct);

    public async Task<RefreshTokenFamily?> GetFamilyForUpdateAsync(Guid familyId, CancellationToken ct)
    {
        LockGuard.RequireTransaction(db, nameof(GetFamilyForUpdateAsync));
        List<RefreshTokenFamily> rows = await db.Set<RefreshTokenFamily>()
            .FromSql($"SELECT * FROM refresh_token_families WHERE id = {familyId} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public Task<RefreshToken?> FindTokenByHashAsync(byte[] tokenHash, CancellationToken ct) =>
        db.Set<RefreshToken>().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task RevokeAllFamiliesForUserAsync(Guid userId, RefreshTokenRevocationReason reason, DateTimeOffset now, CancellationToken ct)
    {
        LockGuard.RequireTransaction(db, nameof(RevokeAllFamiliesForUserAsync));
        return db.Set<RefreshTokenFamily>()
            .Where(f => f.UserId == userId && f.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.RevokedAt, (DateTimeOffset?)now)
                .SetProperty(f => f.RevokedReason, (RefreshTokenRevocationReason?)reason), ct);
    }
}

internal sealed class PasswordResetTokenRepository(EcommerceDbContext db) : IPasswordResetTokenRepository
{
    public void Add(PasswordResetToken token) => db.Set<PasswordResetToken>().Add(token);

    public Task<Guid?> FindUserIdByTokenHashAsync(byte[] tokenHash, CancellationToken ct) =>
        db.Set<PasswordResetToken>().AsNoTracking()
            .Where(t => t.TokenHash == tokenHash)
            .Select(t => (Guid?)t.UserId)
            .FirstOrDefaultAsync(ct);

    public async Task<PasswordResetToken?> GetForUpdateByHashAsync(byte[] tokenHash, CancellationToken ct)
    {
        LockGuard.RequireTransaction(db, nameof(GetForUpdateByHashAsync));
        List<PasswordResetToken> rows = await db.Set<PasswordResetToken>()
            .FromSql($"SELECT * FROM password_reset_tokens WHERE token_hash = {tokenHash} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public Task InvalidateOutstandingAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        LockGuard.RequireTransaction(db, nameof(InvalidateOutstandingAsync));
        return db.Set<PasswordResetToken>()
            .Where(t => t.UserId == userId && t.UsedAt == null && t.InvalidatedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.InvalidatedAt, (DateTimeOffset?)now), ct);
    }

    public Task<bool> HasNewerRequestAsync(Guid userId, DateTimeOffset requestedAt, CancellationToken ct)
    {
        LockGuard.RequireTransaction(db, nameof(HasNewerRequestAsync));
        return db.Set<PasswordResetToken>().AsNoTracking().AnyAsync(t => t.UserId == userId && t.RequestedAt > requestedAt, ct);
    }

    public Task InvalidateOlderAsync(Guid userId, DateTimeOffset requestedAt, DateTimeOffset now, CancellationToken ct)
    {
        LockGuard.RequireTransaction(db, nameof(InvalidateOlderAsync));
        return db.Set<PasswordResetToken>()
            .Where(t => t.UserId == userId && t.UsedAt == null && t.InvalidatedAt == null && (t.RequestedAt == null || t.RequestedAt < requestedAt))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.InvalidatedAt, (DateTimeOffset?)now), ct);
    }
}
