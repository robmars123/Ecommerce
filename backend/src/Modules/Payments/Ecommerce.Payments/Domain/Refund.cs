namespace Ecommerce.Payments.Domain;

/// <summary>A refund request (arch 7.1). Slice 4 creates only the automatic kind; the provider call comes with the outbox consumer (Slice 6).</summary>
internal sealed class Refund
{
    public const string LateSuccess = "late-success";
    public const string ExtraSuccess = "extra-success";

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

    /// <summary>The whole payment, Pending, no actor, key <c>auto:{paymentId}</c> (unique, so a payment is refunded automatically once).</summary>
    public static Refund CreateAutomatic(Guid id, Payment payment, string reason, DateTimeOffset now) =>
        new()
        {
            Id = id, PaymentId = payment.Id, Amount = payment.Amount, Currency = payment.Currency, Status = RefundStatus.Pending,
            Reason = reason, ActorId = null, IdempotencyKey = AutomaticKey(payment.Id), CreatedAt = now,
        };
}
