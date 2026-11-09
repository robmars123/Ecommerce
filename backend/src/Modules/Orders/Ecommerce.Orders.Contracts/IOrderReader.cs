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

/// <summary>One row of the admin dashboard's recent orders: any status, including PendingPayment.</summary>
public sealed record RecentOrder(Guid Id, string OrderNumber, Guid UserId, DateTimeOffset CreatedAt, decimal GrandTotal, string Currency, string Status);

public sealed record RecentOrderPage(IReadOnlyList<RecentOrder> Items, int TotalCount);

/// <summary>Orders' read for other modules: the notification view, and the dashboard's strongly consistent reads (arch 8.8.5).</summary>
public interface IOrderReader
{
    /// <summary>Null when the order does not exist.</summary>
    Task<OrderNotificationView?> GetNotificationViewAsync(Guid orderId, CancellationToken ct);

    /// <summary>Newest first. Pages are 1-based.</summary>
    Task<RecentOrderPage> ListRecentAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>Return requests still waiting for an admin decision.</summary>
    Task<int> CountPendingReturnsAsync(CancellationToken ct);
}
