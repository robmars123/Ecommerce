using Ecommerce.Orders.Domain;

namespace Ecommerce.Orders.Application;

internal sealed record OrderAlert(string Title, string Message);

internal sealed record OrderStatusNote(string PillNote, OrderAlert? Alert);

/// <summary>Customer-facing text for an order: the cancelled/refunded note and alert (P2.50) and each timeline entry's description (P2.53). Derived on read from constants; never stored, and the stored reason is never echoed.</summary>
internal static class OrderStatusNotes
{
    private const string CancelledTitle = "This order was cancelled";
    private const string CancelledGeneric = "This order was cancelled and will not ship.";

    private static readonly OrderStatusNote Expired = new(
        "Payment not completed in time",
        new OrderAlert(
            CancelledTitle,
            "We held your items while you paid, but payment was not completed before the hold ran out, so we released them and cancelled the order. Nothing will ship. If a payment went through after that, we refund it automatically."));

    private static readonly OrderStatusNote CancelledOther = new(
        "Order cancelled", new OrderAlert(CancelledTitle, CancelledGeneric));

    private static readonly OrderStatusNote RefundedInFull = new("Refunded in full to your original payment method", null);

    public static OrderStatusNote? For(OrderStatus status, IReadOnlyList<TimelineRow> timeline)
    {
        switch (status)
        {
            case OrderStatus.Cancelled:
                TimelineRow? entry = timeline.LastOrDefault(t => t.ToStatus == OrderStatus.Cancelled);
                return IsReservationExpiry(entry?.Reason) ? Expired : CancelledOther;
            case OrderStatus.Refunded:
                return RefundedInFull;
            default:
                return null;
        }
    }

    /// <summary>One customer sentence per target status. The reason is only an equality key for the expiry code, never shown.</summary>
    public static string Describe(OrderStatus to, string? reason) => to switch
    {
        OrderStatus.PendingPayment => "We received your order and held your items for payment.",
        OrderStatus.Paid => "Your payment was confirmed.",
        OrderStatus.Processing => "We started preparing your order.",
        OrderStatus.Shipped => "Your order was handed to the carrier.",
        OrderStatus.Delivered => "Your order was delivered.",
        OrderStatus.Cancelled => IsReservationExpiry(reason)
            ? "Payment was not completed before the hold on your items ran out, so we released them and cancelled the order."
            : CancelledGeneric,
        OrderStatus.Refunded => "We refunded the full amount to your original payment method.",
        OrderStatus.Returned => "Your return was refunded in full to your original payment method.",
        _ => throw new ArgumentOutOfRangeException(nameof(to), to, null),
    };

    private static bool IsReservationExpiry(string? reason) => string.Equals(reason, Order.ReservationExpired, StringComparison.Ordinal);
}
