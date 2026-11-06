using Ecommerce.Identity.Domain;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Identity.Application;

internal sealed class RegistrationService(
    IUserRepository users,
    IPasswordHashing hashing,
    ISecureTokenGenerator generator,
    SessionStarter sessions,
    IAccessTokenIssuer accessTokens,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const string EmailConstraint = "ux_users_email";

    public async Task<RegisterOutcome> RegisterAsync(string email, string password, string displayName, CancellationToken ct)
    {
        // Hash before the transaction: PBKDF2 is deliberately slow and must not hold a connection.
        string passwordHash = hashing.Hash(password);
        DateTimeOffset now = clock.UtcNow;
        User user = User.Register(
            Guid.CreateVersion7(now), EmailAddress.Normalize(email), displayName, passwordHash, generator.NewSecurityStamp(), now);

        try
        {
            (string? refreshToken, DateTimeOffset refreshExpiresAt) = await unitOfWork.InTransactionAsync(_ =>
            {
                users.Add(user);
                return Task.FromResult(sessions.StartFamily(user.Id, now));
            }, ct);

            (string? accessToken, DateTimeOffset accessExpiresAt) = accessTokens.Issue(user, clock.UtcNow);
            return new RegisterOutcome.Created(new SessionIssued(
                accessToken, accessExpiresAt, refreshToken, refreshExpiresAt, UserView.From(user)));
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == EmailConstraint)
        {
            return new RegisterOutcome.DuplicateEmail();
        }
    }
}
