using System.Text.Json;
using Ecommerce.Identity.Domain;
using Ecommerce.Notifications.Contracts;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Identity.Application;

/// <summary>
/// PasswordResetRequested (Slice 6 D15): mint the token, store only its hash in a short transaction of its own, then mail the
/// link with no transaction open. A retry after a send failure mints a fresh token; the mail's idempotency key is the event's,
/// so a provider that already took the first send drops the repeat. The plaintext token exists only in the mail body.
/// </summary>
internal sealed class PasswordResetConsumer(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    ISecureTokenGenerator generator,
    IEmailSender sender,
    IUnitOfWork unitOfWork,
    IClock clock,
    TokenLifetimes lifetimes,
    PasswordResetLinkSettings linkSettings) : IOutboxConsumer
{
    public string Name => "identity.password-reset";

    public IReadOnlySet<string> EventTypes { get; } = new HashSet<string> { PasswordResetService.RequestedEvent };

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct)
    {
        PasswordResetService.PasswordResetRequestedPayload payload =
            JsonSerializer.Deserialize<PasswordResetService.PasswordResetRequestedPayload>(message.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        User? user = await users.FindByIdAsync(payload.UserId, ct);
        if (user is null)
        {
            return;
        }

        string plaintext = generator.NewToken();
        DateTimeOffset now = clock.UtcNow;
        // Locking the user serialises this with RequestAsync and with another delivery. A newer request's token already exists:
        // this event is stale, so it mints nothing and becomes Done. Retries of this same event share requested_at, so the token
        // that was already mailed stays valid.
        bool minted = await unitOfWork.InTransactionAsync(async c =>
        {
            await users.FindByIdForUpdateAsync(user.Id, c);
            if (await resetTokens.HasNewerRequestAsync(user.Id, message.OccurredAt, c))
            {
                return false;
            }
            await resetTokens.InvalidateOlderAsync(user.Id, message.OccurredAt, now, c);
            resetTokens.Add(PasswordResetToken.Create(
                Guid.CreateVersion7(now), user.Id, TokenHash.Of(plaintext), now, now + lifetimes.PasswordReset, message.OccurredAt));
            return true;
        }, ct);
        if (!minted)
        {
            return;
        }

        string? link = string.IsNullOrWhiteSpace(linkSettings.LinkBaseUrl)
            ? null
            : $"{linkSettings.LinkBaseUrl}?token={Uri.EscapeDataString(plaintext)}";
        await sender.SendAsync(
            new EmailMessage(
                user.Email,
                "Reset your password",
                $"Hi {user.DisplayName},{Environment.NewLine}{Environment.NewLine}Use this link to choose a new password: {link ?? "(no link base URL is configured)"}{Environment.NewLine}"
                + $"If you did not ask for this, ignore this email.{Environment.NewLine}",
                $"{Name}:{message.EventId}"),
            ct);
    }
}
