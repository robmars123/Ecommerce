using Ecommerce.Identity.Domain;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Identity.Application;

/// <summary>
/// D10. Request: state change first, the link goes out only after commit (rule 6: no external call in a transaction).
/// Confirm: lock the user, then the token (lock order users -> tokens), so two parallel confirms with one token
/// produce exactly one success.
/// </summary>
internal sealed class PasswordResetService(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    IRefreshTokenRepository refreshTokens,
    IPasswordHashing hashing,
    ISecureTokenGenerator generator,
    IPasswordResetLinkSender sender,
    IUnitOfWork unitOfWork,
    IClock clock,
    TokenLifetimes lifetimes,
    PasswordResetLinkSettings linkSettings)
{
    private sealed record Issued(Guid UserId, string Email, string PlaintextToken);

    /// <summary>Always completes the same way for known and unknown emails (no enumeration).</summary>
    public async Task RequestAsync(string email, CancellationToken ct)
    {
        string normalized = EmailAddress.Normalize(email);

        Issued? issued = await unitOfWork.InTransactionAsync(async c =>
        {
            User? user = await users.FindByEmailForUpdateAsync(normalized, c);
            if (user is null)
            {
                return null;
            }

            DateTimeOffset now = clock.UtcNow;
            await resetTokens.InvalidateOutstandingAsync(user.Id, now, c);
            string plaintext = generator.NewToken();
            resetTokens.Add(PasswordResetToken.Create(
                Guid.CreateVersion7(now), user.Id, TokenHash.Of(plaintext), now, now + lifetimes.PasswordReset));
            return new Issued(user.Id, user.Email, plaintext);
        }, ct);

        if (issued is null)
        {
            return;
        }

        string? link = string.IsNullOrWhiteSpace(linkSettings.LinkBaseUrl)
            ? null
            : $"{linkSettings.LinkBaseUrl}?token={Uri.EscapeDataString(issued.PlaintextToken)}";
        await sender.SendAsync(issued.UserId, issued.Email, link, ct);
    }

    public async Task<ResetConfirmOutcome> ConfirmAsync(string token, string newPassword, CancellationToken ct)
    {
        // Hashing is CPU work: do it before taking any lock.
        string newHash = hashing.Hash(newPassword);
        byte[] tokenHash = TokenHash.Of(token);

        Guid? userId = await resetTokens.FindUserIdByTokenHashAsync(tokenHash, ct);
        if (userId is null)
        {
            return new ResetConfirmOutcome.InvalidToken();
        }

        return await unitOfWork.InTransactionAsync<ResetConfirmOutcome>(async c =>
        {
            // Lock order: users, then the token. The first load of each is the FOR UPDATE query.
            User? user = await users.FindByIdForUpdateAsync(userId.Value, c);
            PasswordResetToken? resetToken = await resetTokens.GetForUpdateByHashAsync(tokenHash, c);
            DateTimeOffset now = clock.UtcNow;
            if (user is null || resetToken is null || resetToken.UserId != user.Id || !resetToken.CanRedeem(now))
            {
                return new ResetConfirmOutcome.InvalidToken();
            }

            resetToken.Redeem(now);
            user.ReplacePasswordHash(newHash, generator.NewSecurityStamp(), now);
            await refreshTokens.RevokeAllFamiliesForUserAsync(user.Id, RefreshTokenRevocationReason.PasswordReset, now, c);
            return new ResetConfirmOutcome.Success();
        }, ct);
    }
}
