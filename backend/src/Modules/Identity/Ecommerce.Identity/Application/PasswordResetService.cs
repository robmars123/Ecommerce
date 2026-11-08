using System.Text.Json;
using Ecommerce.Identity.Domain;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Identity.Application;

/// <summary>
/// D10, Slice 6 D15. Request: in one transaction lock the user, invalidate outstanding tokens and stage a PasswordResetRequested
/// outbox row; the token is minted and mailed later by <see cref="PasswordResetConsumer"/>, so no plaintext token is ever stored
/// and no external call runs in a transaction (rule 6). Confirm: lock the user, then the token (lock order users, tokens), so two
/// parallel confirms with one token produce exactly one success.
/// </summary>
internal sealed class PasswordResetService(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    IRefreshTokenRepository refreshTokens,
    IPasswordHashing hashing,
    ISecureTokenGenerator generator,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const string RequestedEvent = "PasswordResetRequested";

    internal sealed record PasswordResetRequestedPayload(Guid UserId);

    /// <summary>Always completes the same way for known and unknown emails (no enumeration).</summary>
    public async Task RequestAsync(string email, CancellationToken ct)
    {
        string normalized = EmailAddress.Normalize(email);

        await unitOfWork.InTransactionAsync(async c =>
        {
            User? user = await users.FindByEmailForUpdateAsync(normalized, c);
            if (user is null)
            {
                return false;
            }

            DateTimeOffset now = clock.UtcNow;
            await resetTokens.InvalidateOutstandingAsync(user.Id, now, c);
            outbox.Add(new OutboxMessage(
                Guid.CreateVersion7(now), RequestedEvent, "User", user.Id, 1,
                JsonSerializer.Serialize(new PasswordResetRequestedPayload(user.Id), new JsonSerializerOptions(JsonSerializerDefaults.Web)), now));
            return true;
        }, ct);
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
