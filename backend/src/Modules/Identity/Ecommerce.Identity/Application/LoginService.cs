using Ecommerce.Identity.Domain;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Identity.Application;

/// <summary>
/// D7. The row lock comes first: with the user row locked, at most MaxFailedAttempts passwords are ever verified,
/// however many guesses arrive in parallel. An unknown email and a locked account both burn one dummy verification
/// and return the same generic failure.
/// </summary>
internal sealed class LoginService(
    IUserRepository users,
    IPasswordHashing hashing,
    SessionStarter sessions,
    IAccessTokenIssuer accessTokens,
    ISecurityEventSink security,
    IUnitOfWork unitOfWork,
    IClock clock,
    LockoutPolicy lockout)
{
    private sealed record Attempt(User? SignedIn, string? RefreshToken, DateTimeOffset RefreshExpiresAt, Guid? LockedOutUserId);

    private static readonly Attempt Rejected = new(null, null, default, null);

    public async Task<LoginOutcome> LoginAsync(string email, string password, CancellationToken ct)
    {
        string normalized = EmailAddress.Normalize(email);

        Attempt attempt = await unitOfWork.InTransactionAsync(async c =>
        {
            User? user = await users.FindByEmailForUpdateAsync(normalized, c);
            if (user is null)
            {
                hashing.VerifyAgainstDummy(password);
                return Rejected;
            }

            DateTimeOffset now = clock.UtcNow;
            if (user.IsLockedOut(now))
            {
                hashing.VerifyAgainstDummy(password);
                return Rejected;
            }

            PasswordVerification verification = hashing.Verify(user.PasswordHash, password);
            if (verification == PasswordVerification.Failed)
            {
                bool locked = user.RecordFailedLogin(now, lockout);
                return locked ? Rejected with { LockedOutUserId = user.Id } : Rejected;
            }

            user.RecordSuccessfulLogin(now);
            if (verification == PasswordVerification.SuccessRehashNeeded)
            {
                user.UpgradePasswordHash(hashing.Hash(password), now);
            }
            (string? refreshToken, DateTimeOffset refreshExpiresAt) = sessions.StartFamily(user.Id, now);
            return new Attempt(user, refreshToken, refreshExpiresAt, null);
        }, ct);

        // Everything below runs after the commit.
        if (attempt.LockedOutUserId is { } lockedOut)
        {
            security.AccountLockedOut(lockedOut);
        }
        if (attempt.SignedIn is not { } signedIn)
        {
            return new LoginOutcome.InvalidCredentials();
        }

        (string? accessToken, DateTimeOffset accessExpiresAt) = accessTokens.Issue(signedIn, clock.UtcNow);
        return new LoginOutcome.Success(new SessionIssued(
            accessToken, accessExpiresAt, attempt.RefreshToken!, attempt.RefreshExpiresAt, UserView.From(signedIn)));
    }
}
