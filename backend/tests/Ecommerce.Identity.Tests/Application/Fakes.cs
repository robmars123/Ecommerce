using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Identity.Tests.Application;

internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

/// <summary>An ordered log shared by the fakes, so tests can assert "X happened before Y".</summary>
internal sealed class EventLog : List<string>;

internal sealed class FakeUnitOfWork(EventLog log) : IUnitOfWork
{
    public int Transactions { get; private set; }

    /// <summary>Simulates a database error raised at commit time (for example a unique violation).</summary>
    public Exception? FailOnCommit { get; set; }

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        Transactions++;
        log.Add("tx-begin");
        try
        {
            T? result = await work(ct);
            if (FailOnCommit is not null)
            {
                throw FailOnCommit;
            }
            log.Add("tx-commit");
            return result;
        }
        catch
        {
            log.Add("tx-rollback");
            throw;
        }
    }
}

internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public void Add(User user) => Users.Add(user);

    public Task<User?> FindByEmailForUpdateAsync(string email, CancellationToken ct) =>
        Task.FromResult(Users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) => FindByIdForUpdateAsync(id, ct);

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct) => FindByEmailForUpdateAsync(email, ct);
}

internal sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshTokenFamily> Families { get; } = [];
    public List<RefreshToken> Tokens { get; } = [];
    public List<(Guid UserId, RefreshTokenRevocationReason Reason)> RevokeAllCalls { get; } = [];

    public void AddFamily(RefreshTokenFamily family) => Families.Add(family);

    public void AddToken(RefreshToken token) => Tokens.Add(token);

    public Task<Guid?> FindFamilyIdByTokenHashAsync(byte[] tokenHash, CancellationToken ct) =>
        Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash.SequenceEqual(tokenHash))?.FamilyId);

    public Task<RefreshTokenFamily?> GetFamilyForUpdateAsync(Guid familyId, CancellationToken ct) =>
        Task.FromResult(Families.FirstOrDefault(f => f.Id == familyId));

    public Task<RefreshToken?> FindTokenByHashAsync(byte[] tokenHash, CancellationToken ct) =>
        Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash.SequenceEqual(tokenHash)));

    public Task RevokeAllFamiliesForUserAsync(Guid userId, RefreshTokenRevocationReason reason, DateTimeOffset now, CancellationToken ct)
    {
        RevokeAllCalls.Add((userId, reason));
        foreach (RefreshTokenFamily? family in Families.Where(f => f.UserId == userId))
        {
            family.Revoke(reason, now);
        }
        return Task.CompletedTask;
    }
}

internal sealed class FakePasswordResetTokenRepository(EventLog log) : IPasswordResetTokenRepository
{
    public List<PasswordResetToken> Tokens { get; } = [];
    public int InvalidateCalls { get; private set; }

    public void Add(PasswordResetToken token)
    {
        log.Add("reset-token-added");
        Tokens.Add(token);
    }

    public Task<Guid?> FindUserIdByTokenHashAsync(byte[] tokenHash, CancellationToken ct) =>
        Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash.SequenceEqual(tokenHash))?.UserId);

    public Task<PasswordResetToken?> GetForUpdateByHashAsync(byte[] tokenHash, CancellationToken ct) =>
        Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash.SequenceEqual(tokenHash)));

    public Task InvalidateOutstandingAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        InvalidateCalls++;
        log.Add("reset-tokens-invalidated");
        foreach (PasswordResetToken? token in Tokens.Where(t => t.UserId == userId))
        {
            token.Invalidate(now);
        }
        return Task.CompletedTask;
    }
}

internal sealed class FakeHashing(EventLog log) : IPasswordHashing
{
    public int DummyVerifications { get; private set; }
    public int RealVerifications { get; private set; }
    public bool RehashNeeded { get; set; }

    public string Hash(string password)
    {
        log.Add("hash");
        return "hash:" + password;
    }

    public PasswordVerification Verify(string hash, string password)
    {
        RealVerifications++;
        if (hash != "hash:" + password)
        {
            return PasswordVerification.Failed;
        }
        return RehashNeeded ? PasswordVerification.SuccessRehashNeeded : PasswordVerification.Success;
    }

    public void VerifyAgainstDummy(string password) => DummyVerifications++;
}

internal sealed class FakeTokenGenerator : ISecureTokenGenerator
{
    private int _tokens;
    private int _stamps;

    public string NewToken() => $"opaque-token-{++_tokens}";

    public string NewSecurityStamp() => $"stamp-{++_stamps}";
}

internal sealed class FakeAccessTokenIssuer(EventLog log) : IAccessTokenIssuer
{
    public (string Token, DateTimeOffset ExpiresAt) Issue(User user, DateTimeOffset now)
    {
        log.Add("issue-access-token");
        return ($"access-{user.Id:N}", now.AddMinutes(15));
    }
}

internal sealed class FakeSecurityEvents : ISecurityEventSink
{
    public List<(Guid UserId, Guid FamilyId)> Reuses { get; } = [];
    public List<Guid> Lockouts { get; } = [];

    public void RefreshTokenReuseDetected(Guid userId, Guid familyId) => Reuses.Add((userId, familyId));

    public void AccountLockedOut(Guid userId) => Lockouts.Add(userId);
}

internal sealed class FakeLinkSender(EventLog log) : IPasswordResetLinkSender
{
    public List<(Guid UserId, string Email, string? Link)> Sent { get; } = [];

    public Task SendAsync(Guid userId, string email, string? link, CancellationToken ct)
    {
        log.Add("send-reset-link");
        Sent.Add((userId, email, link));
        return Task.CompletedTask;
    }
}

/// <summary>Everything a service test needs, wired with fakes and a shared event log.</summary>
internal sealed class IdentityFakes
{
    public static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public IdentityFakes()
    {
        Log = new EventLog();
        Clock = new FakeClock(T0);
        UnitOfWork = new FakeUnitOfWork(Log);
        Users = new FakeUserRepository();
        RefreshTokens = new FakeRefreshTokenRepository();
        ResetTokens = new FakePasswordResetTokenRepository(Log);
        Hashing = new FakeHashing(Log);
        Generator = new FakeTokenGenerator();
        AccessTokens = new FakeAccessTokenIssuer(Log);
        Security = new FakeSecurityEvents();
        Sender = new FakeLinkSender(Log);
        Sessions = new SessionStarter(RefreshTokens, Generator, Lifetimes);
    }

    public static readonly TokenLifetimes Lifetimes =
        new(TimeSpan.FromMinutes(15), TimeSpan.FromDays(14), TimeSpan.FromDays(30), TimeSpan.FromMinutes(30));

    public static readonly LockoutPolicy Lockout = new(5, TimeSpan.FromMinutes(5));

    public EventLog Log { get; }
    public FakeClock Clock { get; }
    public FakeUnitOfWork UnitOfWork { get; }
    public FakeUserRepository Users { get; }
    public FakeRefreshTokenRepository RefreshTokens { get; }
    public FakePasswordResetTokenRepository ResetTokens { get; }
    public FakeHashing Hashing { get; }
    public FakeTokenGenerator Generator { get; }
    public FakeAccessTokenIssuer AccessTokens { get; }
    public FakeSecurityEvents Security { get; }
    public FakeLinkSender Sender { get; }
    public SessionStarter Sessions { get; }

    public User AddUser(string email = "ana@example.test", string password = "correct horse", UserRole role = UserRole.Customer)
    {
        User user = role == UserRole.Admin
            ? User.CreateAdmin(Guid.NewGuid(), email, "Ana", "hash:" + password, "stamp-0", T0)
            : User.Register(Guid.NewGuid(), email, "Ana", "hash:" + password, "stamp-0", T0);
        Users.Users.Add(user);
        return user;
    }

    public LoginService Login() =>
        new(Users, Hashing, Sessions, AccessTokens, Security, UnitOfWork, Clock, Lockout);

    public RegistrationService Registration() =>
        new(Users, Hashing, Generator, Sessions, AccessTokens, UnitOfWork, Clock);

    public RefreshService Refresh() =>
        new(RefreshTokens, Users, Generator, AccessTokens, Security, UnitOfWork, Clock, Lifetimes);

    public PasswordResetService PasswordReset(string? linkBaseUrl = "https://app.test/reset-password") =>
        new(Users, ResetTokens, RefreshTokens, Hashing, Generator, Sender, UnitOfWork, Clock, Lifetimes,
            new PasswordResetLinkSettings(linkBaseUrl));
}
