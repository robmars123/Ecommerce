namespace Ecommerce.Payments.Domain;

/// <summary>A refund request (arch 7.1). Slice 4 creates only the automatic kind; the provider call comes with the outbox consumer (Slice 6).</summary>
internal sealed class Refund
{
    public const string LateSuccess = "late-success";
    public const string ExtraSuccess = "extra-success";
    public const string AmountMismatch = "amount-mismatch";

    private Refund() { }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "";
    public RefundStatus Status { get; private set; }
    public string Reason { get; private set; } = "";

    /// <summary>Null for an automatic refund.</summary>
    public Guid? ActorId { get; private set; }

    public string IdempotencyKey { get; private set; } = "";
    public string? ProviderRefundId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static string AutomaticKey(Guid paymentId) => $"auto:{paymentId}";

    public bool IsPending => Status == RefundStatus.Pending;

    /// <summary>An admin's refund request (arch 8.5 step 3): Pending, with the admin's idempotency key. The ceiling was checked by the caller.</summary>
    public static Refund CreateAdmin(Guid id, Payment payment, decimal amount, string reason, Guid actorId, string idempotencyKey, DateTimeOffset now) =>
        new()
        {
            Id = id, PaymentId = payment.Id, Amount = amount, Currency = payment.Currency, Status = RefundStatus.Pending,
            Reason = reason, ActorId = actorId, IdempotencyKey = idempotencyKey, CreatedAt = now,
        };

    /// <summary>Pending to Succeeded (the verified refund webhook). A second terminal event is a no-op: returns false.</summary>
    public bool Succeed(string? providerRefundId, DateTimeOffset now)
    {
        if (Status != RefundStatus.Pending)
        {
            return false;
        }
        Status = RefundStatus.Succeeded;
        ProviderRefundId ??= providerRefundId;
        CompletedAt = now;
        return true;
    }

    /// <summary>Pending to Failed: its amount is free under the ceiling again. A second terminal event is a no-op: returns false.</summary>
    public bool Fail(DateTimeOffset now)
    {
        if (Status != RefundStatus.Pending)
        {
            return false;
        }
        Status = RefundStatus.Failed;
        CompletedAt = now;
        return true;
    }

    /// <summary>
    /// Pending, no actor, key <c>auto:{paymentId}</c> (unique, so a payment is refunded automatically once). The whole payment,
    /// or, when the provider reported a different amount or currency, exactly what it received.
    /// </summary>
    public static Refund CreateAutomatic(Guid id, Payment payment, string reason, DateTimeOffset now, decimal? receivedAmount = null, string? receivedCurrency = null) =>
        new()
        {
            Id = id, PaymentId = payment.Id, Amount = receivedAmount ?? payment.Amount, Currency = receivedCurrency ?? payment.Currency, Status = RefundStatus.Pending,
            Reason = reason, ActorId = null, IdempotencyKey = AutomaticKey(payment.Id), CreatedAt = now,
        };
}
