namespace Ecommerce.Orders.Domain;

internal sealed class Shipment
{
    private Shipment() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Carrier { get; private set; } = "";
    public string TrackingNumber { get; private set; } = "";
    public DateTimeOffset ShippedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }

    public static Shipment Create(Guid orderId, string carrierCode, string trackingNumber, DateTimeOffset now) =>
        new Shipment
        {
            Id = Guid.CreateVersion7(now),
            OrderId = orderId,
            Carrier = carrierCode,
            TrackingNumber = trackingNumber,
            ShippedAt = now,
        };

    public void MarkDelivered(DateTimeOffset now) => DeliveredAt ??= now;
}
