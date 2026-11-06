using Ecommerce.Identity.Application;
using Ecommerce.SharedKernel.Time;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Identity.Infrastructure.PasswordReset;

/// <summary>
/// Development only: writes the reset link to a text file, the way an SMTP pickup directory works. No external call.
/// The link is the only place the plaintext token ever appears, and it is never logged.
/// </summary>
internal sealed class DevPickupDirectoryLinkSender(
    IOptions<PasswordResetOptions> options,
    IHostEnvironment environment,
    IClock clock,
    ILogger<DevPickupDirectoryLinkSender> logger) : IPasswordResetLinkSender
{
    public async Task SendAsync(Guid userId, string email, string? link, CancellationToken ct)
    {
        try
        {
            string directory = Path.GetFullPath(options.Value.DevPickupDirectory, environment.ContentRootPath);
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, $"reset-{clock.UtcNow:yyyyMMddHHmmssfff}-{userId:N}.txt");
            await File.WriteAllTextAsync(
                file,
                $"Password reset link for user {userId} ({clock.UtcNow:O}){Environment.NewLine}{link ?? "(no LinkBaseUrl configured)"}{Environment.NewLine}",
                ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Must not throw (an error only for existing accounts would reveal them). The exception text may hold a path, not a token.
            logger.LogWarning(ex, "Could not write the dev password reset file for user {UserId}", userId);
        }
    }
}
