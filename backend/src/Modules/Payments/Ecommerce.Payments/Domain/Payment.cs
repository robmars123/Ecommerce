namespace Ecommerce.Payments.Domain;

/// <summary>One payment attempt on an order (arch 7.1, 7.3). Only a verified webhook calls Succeed and Fail.</summary>
internal sealed class Payment
{
    public const string DefaultProvider = "stripe";

    private Payment() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public int Attempt { get; private set; }
    public string Provider { get; private set; } = "";
    public string? ProviderPaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "";
    public PaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The attempt has a provider intent that nothing has paid: a cancel request is due when it is superseded or the order ends.</summary>
    public bool HasCancellableIntent => ProviderPaymentId is not null && (Status == PaymentStatus.Pending || Status == PaymentStatus.Failed);

    public static Payment Create(Guid id, Guid orderId, int attempt, decimal amount, string currency, DateTimeOffset now) =>
        new()
        {
            Id = id, OrderId = orderId, Attempt = attempt, Provider = DefaultProvider, Amount = amount, Currency = currency,
            Status = PaymentStatus.Pending, CreatedAt = now, UpdatedAt = now,
        };

    /// <summary>
    /// Pending or Failed to Succeeded (a declined intent the customer retried in the payment sheet can still succeed, D8).
    /// An already succeeded payment is a no-op: returns false.
    /// </summary>
    public bool Succeed(DateTimeOffset now)
    {
        if (Status is not (PaymentStatus.Pending or PaymentStatus.Failed))
        {
            return false;
        }
        Status = PaymentStatus.Succeeded;
        UpdatedAt = now;
        return true;
    }

    /// <summary>Pending to Failed. A late or repeated failure never downgrades any other status: returns false.</summary>
    public bool Fail(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
        {
            return false;
        }
        Status = PaymentStatus.Failed;
        UpdatedAt = now;
        return true;
    }
}
