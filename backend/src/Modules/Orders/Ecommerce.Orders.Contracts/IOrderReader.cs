namespace Ecommerce.Orders.Contracts;

public sealed record OrderNotificationLine(string Name, int Quantity, decimal UnitPrice);

public sealed record OrderNotificationShipment(string CarrierName, string TrackingNumber, string? TrackingUrl);

/// <summary>What an order email needs. Totals are the order's own snapshot.</summary>
public sealed record OrderNotificationView(
    string OrderNumber,
    string Currency,
    decimal GrandTotal,
    IReadOnlyList<OrderNotificationLine> Lines,
    OrderNotificationShipment? LatestShipment);

/// <summary>Orders' read for Notifications (one consumer). Null when the order does not exist.</summary>
public interface IOrderReader
{
    Task<OrderNotificationView?> GetNotificationViewAsync(Guid orderId, CancellationToken ct);
}
