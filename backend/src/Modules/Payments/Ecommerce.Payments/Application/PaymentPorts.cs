using Ecommerce.Payments.Domain;

namespace Ecommerce.Payments.Application;

/// <summary>Payments' persistence port. <see cref="LockByProviderIdAsync"/> needs an open transaction and locks the row FOR UPDATE (D3).</summary>
internal interface IPaymentStore
{
    void Add(Payment payment);

    void AddRefund(Refund refund);

    /// <summary>No-tracking read.</summary>
    Task<Payment?> GetAsync(Guid paymentId, CancellationToken ct);

    /// <summary>The highest attempt of the order, no-tracking.</summary>
    Task<Payment?> GetLatestAsync(Guid orderId, CancellationToken ct);

    /// <summary>The order's other attempts that have a provider intent and did not succeed (cancel candidates), no-tracking.</summary>
    Task<IReadOnlyList<Payment>> ListOpenIntentsAsync(Guid orderId, Guid exceptPaymentId, CancellationToken ct);

    /// <summary>The tracked payment with that provider id, FOR UPDATE, or null.</summary>
    Task<Payment?> LockByProviderIdAsync(string providerPaymentId, CancellationToken ct);

    /// <summary>Sets the provider id only if none is stored yet. Returns the id now stored.</summary>
    Task<string?> AttachProviderIdAsync(Guid paymentId, string providerPaymentId, DateTimeOffset now, CancellationToken ct);
}

internal sealed record IntentRequest(string IdempotencyKey, long AmountMinor, string Currency, Guid OrderId, Guid PaymentId);

internal sealed record CreatedIntent(string ProviderPaymentId, string ClientSecret);

/// <summary>The payment provider (arch 3.2 L): one implementation per provider, a fake in tests. Throws PaymentProviderUnavailableException on failure.</summary>
internal interface IPaymentGateway
{
    Task<CreatedIntent> CreateIntentAsync(IntentRequest request, CancellationToken ct);

    Task<string> GetClientSecretAsync(string providerPaymentId, CancellationToken ct);
}

internal enum PaymentEventKind
{
    Other,
    Succeeded,
    Failed,
    Canceled,
}

/// <summary>The three fields the webhook reads (D7). <see cref="ProviderPaymentId"/> is null for <see cref="PaymentEventKind.Other"/>.</summary>
internal sealed record PaymentEvent(string EventId, string EventType, PaymentEventKind Kind, string? ProviderPaymentId);

internal interface IWebhookVerifier
{
    /// <summary>False for a missing, bad or stale signature, a missing secret, or a body that is not an event.</summary>
    bool TryVerify(string body, string? signatureHeader, DateTimeOffset now, out PaymentEvent? paymentEvent);
}

/// <summary>Operational alerts (logs now, a real channel later). Ids only, never amounts of card data.</summary>
internal interface IPaymentAlerts
{
    /// <summary>Money arrived that no order can use and was refunded automatically (rules 8 and 12). Critical.</summary>
    void AutoRefundIssued(Guid paymentId, Guid orderId, Guid refundId, string reason);

    void UnknownIntent(string eventId, string providerPaymentId);
}
