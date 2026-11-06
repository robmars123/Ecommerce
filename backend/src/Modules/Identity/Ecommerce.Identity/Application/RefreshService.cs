using Ecommerce.Identity.Domain;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Identity.Application;

/// <summary>
/// D5 + D12. Rotation with family reuse detection. The family row lock serializes every refresh of one family, so of
/// N concurrent refreshes with the same token exactly one rotates and the rest are reuse (the family is then revoked).
/// The first load in the locking flow is the FOR UPDATE on the family; the earlier lookup is an untracked projection.
/// </summary>
internal sealed class RefreshService(
    IRefreshTokenRepository tokens,
    IUserRepository users,
    ISecureTokenGenerator generator,
    IAccessTokenIssuer accessTokens,
    ISecurityEventSink security,
    IUnitOfWork unitOfWork,
    IClock clock,
    TokenLifetimes lifetimes)
{
    private sealed record Result(User? User, string? RefreshToken, DateTimeOffset RefreshExpiresAt, (Guid UserId, Guid FamilyId)? Reuse);

    private static readonly Result Rejected = new(null, null, default, null);

    public async Task<RefreshOutcome> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        byte[] hash = TokenHash.Of(refreshToken);
        Guid? familyId = await tokens.FindFamilyIdByTokenHashAsync(hash, ct);
        if (familyId is null)
        {
            return new RefreshOutcome.Rejected();
        }

        Result result = await unitOfWork.InTransactionAsync(async c =>
        {
            RefreshTokenFamily? family = await tokens.GetFamilyForUpdateAsync(familyId.Value, c);
            RefreshToken? presented = await tokens.FindTokenByHashAsync(hash, c);
            if (family is null || presented is null || presented.FamilyId != family.Id)
            {
                return Rejected;
            }

            DateTimeOffset now = clock.UtcNow;
            switch (RefreshRotation.Decide(family, presented, now))
            {
                case RotationOutcome.Reuse:
                    // The revocation must commit even though this request fails.
                    family.Revoke(RefreshTokenRevocationReason.Reuse, now);
                    return Rejected with { Reuse = (family.UserId, family.Id) };

                case RotationOutcome.Rotate:
                    // Re-read the user so a role change takes effect at refresh. A plain read: it is not part of the lock order.
                    User? user = await users.FindByIdAsync(family.UserId, c);
                    if (user is null)
                    {
                        return Rejected;
                    }
                    string newPlaintext = generator.NewToken();
                    RefreshToken child = RefreshRotation.Rotate(
                        family, presented, Guid.CreateVersion7(now), TokenHash.Of(newPlaintext), now, lifetimes.RefreshSliding);
                    tokens.AddToken(child);
                    return new Result(user, newPlaintext, child.ExpiresAt, null);

                default:
                    // FamilyRevoked or Expired: rejected, and nothing to revoke.
                    return Rejected;
            }
        }, ct);

        if (result.Reuse is { } reuse)
        {
            security.RefreshTokenReuseDetected(reuse.UserId, reuse.FamilyId);
        }
        if (result.User is not { } user2)
        {
            return new RefreshOutcome.Rejected();
        }

        (string? accessToken, DateTimeOffset accessExpiresAt) = accessTokens.Issue(user2, clock.UtcNow);
        return new RefreshOutcome.Success(new SessionIssued(
            accessToken, accessExpiresAt, result.RefreshToken!, result.RefreshExpiresAt, UserView.From(user2)));
    }

    /// <summary>Always succeeds, so a client can never tell a valid token from a garbage one.</summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        Guid? familyId = await tokens.FindFamilyIdByTokenHashAsync(TokenHash.Of(refreshToken), ct);
        if (familyId is null)
        {
            return;
        }

        await unitOfWork.InTransactionAsync(async c =>
        {
            RefreshTokenFamily? family = await tokens.GetFamilyForUpdateAsync(familyId.Value, c);
            family?.Revoke(RefreshTokenRevocationReason.Logout, clock.UtcNow);
            return 0;
        }, ct);
    }
}
