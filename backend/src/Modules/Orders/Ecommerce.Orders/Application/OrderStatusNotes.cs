using Ecommerce.Orders.Domain;

namespace Ecommerce.Orders.Application;

internal sealed record OrderAlert(string Title, string Message);

internal sealed record OrderStatusNote(string PillNote, OrderAlert? Alert);

/// <summary>Customer-facing text for a cancelled or refunded order (P2.50). Derived on read from the status and the timeline reason; never stored.</summary>
internal static class OrderStatusNotes
{
    private const string CancelledTitle = "This order was cancelled";

    private static readonly OrderStatusNote Expired = new(
        "Payment not completed in time",
        new OrderAlert(
            CancelledTitle,
            "We held your items while you paid, but payment was not completed before the hold ran out, so we released them and cancelled the order. Nothing will ship. If a payment went through after that, we refund it automatically."));

    private static readonly OrderStatusNote CancelledOther = new(
        "Order cancelled", new OrderAlert(CancelledTitle, "This order was cancelled and will not ship."));

    private static readonly OrderStatusNote RefundedInFull = new("Refunded in full to your original payment method", null);

    public static OrderStatusNote? For(OrderStatus status, IReadOnlyList<TimelineRow> timeline)
    {
        switch (status)
        {
            case OrderStatus.Cancelled:
                TimelineRow? entry = timeline.LastOrDefault(t => t.ToStatus == OrderStatus.Cancelled);
                return string.Equals(entry?.Reason, Order.ReservationExpired, StringComparison.Ordinal) ? Expired : CancelledOther;
            case OrderStatus.Refunded:
                return RefundedInFull;
            default:
                return null;
        }
    }
}
