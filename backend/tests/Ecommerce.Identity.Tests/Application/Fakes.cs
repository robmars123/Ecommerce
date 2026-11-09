using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;
using Ecommerce.Notifications.Contracts;
using Ecommerce.SharedKernel.Outbox;
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
    public Task<IReadOnlyList<User>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<User>>(Users.Where(u => ids.Contains(u.Id)).ToList());

    public List<User> Users { get; } = [];

    public void Add(User user) => Users.Add(user);

    public Task<User?> FindByEmailForUpdateAsync(string email, CancellationToken ct) =>
        Task.FromResult(Users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) => FindByIdForUpdateAsync(id, ct);

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct) => FindByEmailForUpdateAsync(email, ct);
    public Task<Guid?> FindIdByEmailAsync(string normalizedEmail, CancellationToken ct) => throw new NotSupportedException();
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

    public Task<bool> HasNewerRequestAsync(Guid userId, DateTimeOffset requestedAt, CancellationToken ct) =>
        Task.FromResult(Tokens.Any(t => t.UserId == userId && t.RequestedAt > requestedAt));

    public Task InvalidateOlderAsync(Guid userId, DateTimeOffset requestedAt, DateTimeOffset now, CancellationToken ct)
    {
        foreach (PasswordResetToken token in Tokens.Where(t => t.UserId == userId && (t.RequestedAt == null || t.RequestedAt < requestedAt)))
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

internal sealed class FakeEmailSender(EventLog log) : IEmailSender
{
    private readonly Dictionary<string, EmailMessage> _byKey = [];

    public IReadOnlyList<EmailMessage> Sent => _byKey.Values.ToList();
    public int Attempts { get; private set; }
    public int FailNext { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        Attempts++;
        if (FailNext > 0)
        {
            FailNext--;
            throw new EmailProviderUnavailableException("fake provider down");
        }
        log.Add("send-email");
        _byKey.TryAdd(message.IdempotencyKey, message);
        return Task.CompletedTask;
    }
}

internal sealed class FakeOutbox(EventLog log) : IOutbox
{
    public List<OutboxMessage> Messages { get; } = [];

    public void Add(OutboxMessage message)
    {
        log.Add("outbox-added");
        Messages.Add(message);
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
        Sender = new FakeEmailSender(Log);
        Outbox = new FakeOutbox(Log);
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
    public FakeEmailSender Sender { get; }
    public FakeOutbox Outbox { get; }
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
        new(Users, ResetTokens, RefreshTokens, Hashing, Generator, Outbox, UnitOfWork, Clock);

    public PasswordResetConsumer ResetConsumer(string? linkBaseUrl = "https://app.test/reset-password") =>
        new(Users, ResetTokens, Generator, Sender, UnitOfWork, Clock, Lifetimes, new PasswordResetLinkSettings(linkBaseUrl));

    /// <summary>Runs the consumer for every staged password reset event (what the outbox worker does).</summary>
    public async Task DeliverAsync(string? linkBaseUrl)
    {
        foreach (OutboxMessage message in Outbox.Messages.Where(m => m.EventType == PasswordResetService.RequestedEvent && !Delivered.Contains(m.EventId)).ToList())
        {
            await ResetConsumer(linkBaseUrl).HandleAsync(message, CancellationToken.None);
            Delivered.Add(message.EventId);
        }
    }
    private HashSet<Guid> Delivered { get; } = [];

    public string TokenOf(int index) =>
        LinkOf(Sender.Sent.Where(m => m.Subject == "Reset your password").ElementAt(index))[(LinkOf(Sender.Sent.First(m => m.Subject == "Reset your password")).IndexOf("token=", StringComparison.Ordinal) + 6)..];

    public string LinkOf(EmailMessage mail)
    {
        int start = mail.TextBody.IndexOf("https://", StringComparison.Ordinal);
        int end = mail.TextBody.IndexOfAny([' ', '\r', '\n'], start);
        return mail.TextBody[start..end];
    }
}
