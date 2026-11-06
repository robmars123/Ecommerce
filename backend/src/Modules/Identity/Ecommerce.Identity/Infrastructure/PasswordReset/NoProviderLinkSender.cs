using Ecommerce.Identity.Application;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Identity.Infrastructure.PasswordReset;

/// <summary>
/// Every environment except local development: no email provider is chosen (architecture 2), so nothing is sent.
/// Logs the user id only. Slice 6 replaces this seam with an outbox event.
/// </summary>
internal sealed class NoProviderLinkSender(ILogger<NoProviderLinkSender> logger) : IPasswordResetLinkSender
{
    public Task SendAsync(Guid userId, string email, string? link, CancellationToken ct)
    {
        logger.LogWarning("Password reset requested for user {UserId}, but no email provider is configured; nothing was sent", userId);
        return Task.CompletedTask;
    }
}
