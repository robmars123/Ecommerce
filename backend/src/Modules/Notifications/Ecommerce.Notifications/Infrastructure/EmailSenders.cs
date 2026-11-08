using System.Text;
using Ecommerce.Notifications.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Notifications.Infrastructure;

internal sealed class NotificationsOptions
{
    /// <summary>Development only: a directory the dev sender writes one file per idempotency key into. Startup fails if set elsewhere.</summary>
    public string DevPickupDirectory { get; set; } = "";
}

/// <summary>
/// Development only: writes the email to a text file, the way an SMTP pickup directory works. One file per idempotency key, so a
/// resend overwrites instead of duplicating. No external call; the body (which may hold a reset link) is never logged.
/// </summary>
internal sealed class DevPickupDirectoryEmailSender(
    IOptions<NotificationsOptions> options, IHostEnvironment environment, ILogger<DevPickupDirectoryEmailSender> logger) : IEmailSender
{
    public static string FileNameOf(string idempotencyKey)
    {
        StringBuilder name = new();
        foreach (char c in idempotencyKey)
        {
            name.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '.' ? c : '_');
        }
        return name + ".txt";
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        try
        {
            string directory = Path.GetFullPath(options.Value.DevPickupDirectory, environment.ContentRootPath);
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(
                Path.Combine(directory, FileNameOf(message.IdempotencyKey)),
                $"To: {message.To}{Environment.NewLine}Subject: {message.Subject}{Environment.NewLine}{Environment.NewLine}{message.TextBody}",
                ct);
            logger.LogInformation("Dev email written for key {IdempotencyKey}", message.IdempotencyKey);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new EmailProviderUnavailableException("The dev mail directory could not be written.", ex);
        }
    }
}

/// <summary>
/// Every environment except local development: no email provider is chosen (arch 2, open decision). It throws, so mail retries and
/// then dead-letters with an alert: never silent, never lost.
/// </summary>
internal sealed class NoProviderEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct) =>
        throw new EmailProviderUnavailableException("No email provider is configured.");
}
