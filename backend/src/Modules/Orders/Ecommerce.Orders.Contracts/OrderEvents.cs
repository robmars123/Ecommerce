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
