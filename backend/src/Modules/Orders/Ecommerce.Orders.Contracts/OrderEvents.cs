namespace Ecommerce.Orders.Contracts;

/// <summary>The outbox event types Orders writes: exactly one per order transition (arch 8.4, Slice 6 D5).</summary>
public static class OrderEventTypes
{
    public const string OrderPaid = "OrderPaid";
    public const string OrderShipped = "OrderShipped";

    /// <summary>Every transition that is neither Paid nor Shipped: process, deliver, expiry cancel, refund, return, override.</summary>
    public const string OrderStatusChanged = "OrderStatusChanged";
}

/// <summary>The common part of every order event payload. Consumers read more from <see cref="IOrderReader"/>, never from the event.</summary>
public sealed record OrderEventPayload(Guid OrderId, Guid UserId, string FromStatus, string ToStatus, DateTimeOffset OccurredAt);

/// <summary>One line of an OrderPaid event: what the stock commit used.</summary>
public sealed record OrderPaidLine(Guid VariantId, int Quantity);

/// <summary>The OrderPaid payload (arch 6.1). Projectors rebuild from this, never from the order's current state, so a replay gives the same result.</summary>
public sealed record OrderPaidPayload(
    Guid OrderId, string OrderNumber, Guid UserId, decimal GrandTotal, string Currency, IReadOnlyList<OrderPaidLine> Items,
    string FromStatus, string ToStatus, DateTimeOffset OccurredAt);

public static class FulfillmentEventTypes
{
    /// <summary>An order that cannot be fulfilled: its compensation (a full refund) is owed (arch 8.8.4).</summary>
    public const string FulfillmentFailed = "FulfillmentFailed";
}

/// <summary>The FulfillmentFailed payload. <see cref="RefundAmount"/> is fixed when the saga starts, so a retry asks for the same refund.</summary>
public sealed record FulfillmentFailedPayload(
    Guid SagaId, Guid OrderId, Guid ActorId, decimal RefundAmount, string Currency, string Reason, DateTimeOffset OccurredAt);
