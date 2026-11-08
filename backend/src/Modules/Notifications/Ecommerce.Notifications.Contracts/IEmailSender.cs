namespace Ecommerce.Notifications.Contracts;

/// <summary>
/// One email. <see cref="IdempotencyKey"/> is "{consumer}:{eventId}": a provider that supports it drops a second send with the
/// same key, so a worker crash between the send and its mark never mails twice (arch 8.4).
/// </summary>
public sealed record EmailMessage(string To, string Subject, string TextBody, string IdempotencyKey);

/// <summary>No email provider is configured or it could not be reached. The caller's outbox row retries, then dead-letters.</summary>
public sealed class EmailProviderUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The only way modules send mail. Implemented by Notifications; a provider replaces the placeholder senders there.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
