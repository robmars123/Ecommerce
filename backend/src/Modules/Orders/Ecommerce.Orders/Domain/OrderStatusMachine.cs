namespace Ecommerce.Orders.Domain;

/// <summary>
/// The only legal order transitions (arch 7.3), as data. Callers: this slice uses the three fulfillment edges and the
/// override; Slice 4 (webhook, expiry) and Slice 6 (refund, return) reuse the same table.
/// </summary>
internal static class OrderStatusMachine
{
    private static readonly HashSet<(OrderStatus From, OrderStatus To)> LegalEdges =
    [
        (OrderStatus.PendingPayment, OrderStatus.Paid),
        (OrderStatus.PendingPayment, OrderStatus.Cancelled),
        (OrderStatus.Paid, OrderStatus.Processing),
        (OrderStatus.Paid, OrderStatus.Refunded),
        (OrderStatus.Processing, OrderStatus.Shipped),
        (OrderStatus.Processing, OrderStatus.Refunded),
        (OrderStatus.Shipped, OrderStatus.Delivered),
        (OrderStatus.Delivered, OrderStatus.Returned),
    ];

    // Slice 5 D4: an override may not touch an edge owned by a money, stock or payment path. Shipped needs a shipment row
    // (use ship), Paid needs the verified webhook, Cancelled needs the reservation release, Refunded/Returned need money.
    private static readonly HashSet<(OrderStatus From, OrderStatus To)> OverridableEdges =
    [
        (OrderStatus.Paid, OrderStatus.Processing),
        (OrderStatus.Shipped, OrderStatus.Delivered),
    ];

    public static bool IsLegal(OrderStatus from, OrderStatus to) => LegalEdges.Contains((from, to));

    public static bool IsOverridable(OrderStatus from, OrderStatus to) => OverridableEdges.Contains((from, to));
}
