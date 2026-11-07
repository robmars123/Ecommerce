namespace Ecommerce.Payments.Contracts;

public sealed record PaymentAttemptInfo(Guid PaymentId, int Attempt, string Status, string? ProviderPaymentId);

/// <summary>The provider's intent and the secret the client confirms with. The secret is never stored.</summary>
public sealed record PaymentIntentHandle(string ProviderPaymentId, string ClientSecret, int Attempt);

/// <summary>The payment provider could not be reached or is not configured. The order is untouched; repeat the request.</summary>
public sealed class PaymentProviderUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Payments' side of checkout and retry (arch 8.2 rules 1, 4, 11). <see cref="StartAttemptAsync"/> and
/// <see cref="RequestCancelOpenIntentAsync"/> run inside the caller's transaction and call no provider.
/// <see cref="EnsureIntentAsync"/> calls the provider and so must run with no transaction open (rule 6).
/// </summary>
public interface IPaymentAttempts
{
    /// <summary>Adds the next attempt (Pending, the given amount). If the previous attempt has an intent and did not succeed, stages a cancel request for it.</summary>
    Task<PaymentAttemptInfo> StartAttemptAsync(Guid paymentId, Guid orderId, decimal amount, string currency, CancellationToken ct);

    /// <summary>Stages a cancel request for the latest attempt's intent, if it has one and did not succeed.</summary>
    Task RequestCancelOpenIntentAsync(Guid orderId, CancellationToken ct);

    Task<PaymentAttemptInfo?> GetLatestAsync(Guid orderId, CancellationToken ct);

    /// <summary>Retrieves the client secret of the stored intent, or creates the intent (idempotent key per order and attempt) and stores its id.</summary>
    Task<PaymentIntentHandle> EnsureIntentAsync(Guid paymentId, CancellationToken ct);
}
