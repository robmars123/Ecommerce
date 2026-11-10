using Ecommerce.Payments.Domain;

namespace Ecommerce.Payments.Application;

/// <summary>Payments' persistence port. <see cref="LockByProviderIdAsync"/> needs an open transaction and locks the row FOR UPDATE (D3).</summary>
internal interface IPaymentStore
{
    void Add(Payment payment);

    void AddRefund(Refund refund);

    /// <summary>The order's paying payment (captured, not an automatic-refund extra), highest attempt, tracked, FOR UPDATE (needs an open transaction), or null.</summary>
    Task<Payment?> LockPayingPaymentAsync(Guid orderId, CancellationToken ct);

    /// <summary>The tracked payment FOR UPDATE (needs an open transaction), or null.</summary>
    Task<Payment?> LockByIdAsync(Guid paymentId, CancellationToken ct);

    /// <summary>The paying payment, no-tracking, no lock, or null.</summary>
    Task<Payment?> GetPayingAsync(Guid orderId, CancellationToken ct);

    /// <summary>Sum of the payment's refunds in these statuses, skipping the given refund ids.</summary>
    Task<decimal> SumRefundsAsync(Guid paymentId, IReadOnlyCollection<RefundStatus> statuses, IReadOnlyCollection<Guid> except, CancellationToken ct);

    /// <summary>All of the payment's refunds, oldest first, no-tracking.</summary>
    Task<IReadOnlyList<Refund>> ListRefundsAsync(Guid paymentId, CancellationToken ct);

    /// <summary>Refunds of any payment of these orders: each with its order id, plus the count and the Succeeded-with-actor sum. Newest first, at most <paramref name="recentTake"/> rows. No-tracking.</summary>
    Task<OrderRefundRows> ListRefundsForOrdersAsync(IReadOnlyCollection<Guid> orderIds, int recentTake, CancellationToken ct);

    /// <summary>No-tracking lookup by our refund id (metadata) or the provider refund id.</summary>
    Task<Refund?> FindRefundAsync(Guid? refundId, string? providerRefundId, CancellationToken ct);

    /// <summary>The tracked refund by id, or null. Call after the payment lock is held.</summary>
    Task<Refund?> GetRefundTrackedAsync(Guid refundId, CancellationToken ct);

    /// <summary>Sets the provider refund id only if none is stored yet (a single statement).</summary>
    Task SetProviderRefundIdAsync(Guid refundId, string providerRefundId, CancellationToken ct);

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

internal sealed record OrderRefundRow(Refund Refund, Guid OrderId);

internal sealed record OrderRefundRows(decimal ActorSucceededTotal, int Count, IReadOnlyList<OrderRefundRow> Recent);

internal sealed record IntentRequest(string IdempotencyKey, long AmountMinor, string Currency, Guid OrderId, Guid PaymentId);

internal sealed record CreatedIntent(string ProviderPaymentId, string ClientSecret);

internal sealed record RefundCall(string ProviderPaymentId, long AmountMinor, string Currency, Guid RefundId, string IdempotencyKey);

/// <summary>The payment provider (arch 3.2 L): one implementation per provider, a fake in tests. Throws PaymentProviderUnavailableException on failure.</summary>
internal interface IPaymentGateway
{
    Task<CreatedIntent> CreateIntentAsync(IntentRequest request, CancellationToken ct);

    Task<string> GetClientSecretAsync(string providerPaymentId, CancellationToken ct);

    /// <summary>Creates the provider refund; the idempotency key makes a repeat return the same refund. Returns the provider refund id.</summary>
    Task<string> CreateRefundAsync(RefundCall call, CancellationToken ct);

    /// <summary>Cancels the intent. An intent that is already canceled or succeeded counts as done and does not throw.</summary>
    Task CancelIntentAsync(string providerPaymentId, CancellationToken ct);
}

internal enum PaymentEventKind
{
    Other,
    Succeeded,
    Failed,
    Canceled,
    Refund,
}

internal enum RefundNoticeStatus
{
    Pending,
    Succeeded,
    Failed,
}

/// <summary>One refund a refund event reports: our id (metadata.refund_id) and/or the provider refund id, and its state.</summary>
internal sealed record RefundNotice(Guid? RefundId, string? ProviderRefundId, RefundNoticeStatus Status);

/// <summary>The fields the webhook reads (D7). <see cref="ProviderPaymentId"/> is null for refund and other events; amount received (minor units) and currency are read for a success only.</summary>
internal sealed record PaymentEvent(
    string EventId, string EventType, PaymentEventKind Kind, string? ProviderPaymentId, IReadOnlyList<RefundNotice>? Refunds = null,
    long? AmountReceived = null, string? Currency = null);

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

    /// <summary>Creating the provider intent failed (the caller answers 503 and the client retries). Ids and the provider error type/code only, never a secret or an amount.</summary>
    void IntentCreationFailed(Guid orderId, Guid paymentId, int attempt, Exception? providerError);

    /// <summary>A refund failed at the provider; its amount is free again. Critical: someone must look.</summary>
    void RefundFailed(Guid refundId, Guid paymentId);

    /// <summary>A full refund succeeded but the order could not move (wrong status). The money is out; look at the order.</summary>
    void RefundOrderNotTransitioned(Guid refundId, Guid orderId);
}
