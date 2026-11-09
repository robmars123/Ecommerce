namespace Ecommerce.Payments.Contracts;

public static class PaymentEventTypes
{
    /// <summary>A refund an actor asked for (an admin or the fulfillment saga) succeeded. Automatic refunds never emit it (arch 8.8, Slice 6b D12).</summary>
    public const string RefundSucceeded = "RefundSucceeded";
}

/// <summary>The RefundSucceeded payload. Aggregate Refund, version 2 (version 1 is RefundRequested).</summary>
public sealed record RefundSucceededPayload(Guid RefundId, Guid OrderId, decimal Amount, string Currency, DateTimeOffset SucceededAt);
