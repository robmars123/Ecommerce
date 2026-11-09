namespace Ecommerce.Orders.Domain;

/// <summary>
/// The compensation record of an order that cannot be fulfilled (arch 8.8.4, Slice 6b D17). One per order (unique). It is created
/// Compensating with the refund amount fixed at that moment, and becomes Compensated when the full refund lands.
/// </summary>
internal sealed class FulfillmentSaga
{
    public const string FulfillStep = "Fulfill";
    public const string Compensating = "Compensating";
    public const string Compensated = "Compensated";

    private FulfillmentSaga() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Step { get; private set; } = FulfillStep;
    public string Status { get; private set; } = Compensating;
    public string Reason { get; private set; } = "";
    public Guid ActorId { get; private set; }
    public decimal RefundAmount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static FulfillmentSaga Start(Guid orderId, Guid actorId, string reason, decimal refundAmount, DateTimeOffset now) =>
        new() { Id = Guid.CreateVersion7(now), OrderId = orderId, ActorId = actorId, Reason = reason, RefundAmount = refundAmount, CreatedAt = now };

    public void MarkCompensated() => Status = Compensated;
}
