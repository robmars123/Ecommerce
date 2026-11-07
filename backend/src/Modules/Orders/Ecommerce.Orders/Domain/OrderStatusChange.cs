namespace Ecommerce.Orders.Domain;

/// <summary>One row of the customer's timeline. Exactly one per transition; never edited or deleted.</summary>
internal sealed class OrderStatusChange
{
    private OrderStatusChange() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public OrderStatus? FromStatus { get; private set; }
    public OrderStatus ToStatus { get; private set; }
    public Guid? ChangedBy { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static OrderStatusChange Create(Guid orderId, OrderStatus? from, OrderStatus to, Guid? changedBy, string? reason, DateTimeOffset now) =>
        new OrderStatusChange
        {
            Id = Guid.CreateVersion7(now),
            OrderId = orderId,
            FromStatus = from,
            ToStatus = to,
            ChangedBy = changedBy,
            Reason = reason,
            CreatedAt = now,
        };
}
