using Ecommerce.Orders.Application;
using Ecommerce.Orders.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Orders.Tests.Application;

/// <summary>P2.50: the customer-facing status note and cancellation alert, derived from the status and its timeline reason.</summary>
public class OrderStatusNotesTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private const string ExpiredMessage =
        "We held your items while you paid, but payment was not completed before the hold ran out, so we released them and cancelled the order. Nothing will ship. If a payment went through after that, we refund it automatically.";

    private static IReadOnlyList<TimelineRow> Cancelled(string? reason) =>
    [
        new TimelineRow(null, OrderStatus.PendingPayment, Now, null),
        new TimelineRow(OrderStatus.PendingPayment, OrderStatus.Cancelled, Now.AddMinutes(16), reason),
    ];

    [Fact]
    public void Expired_cancellation_from_the_real_domain_gives_the_expiry_note_and_alert()
    {
        Guid id = Guid.NewGuid();
        OrderItem item = OrderItem.Create(Guid.NewGuid(), id, Guid.NewGuid(), "TEE-1", "Tee", 10.00m, 2);
        OrderTotals totals = OrderTotals.Compute([item.LineTotal], 5.00m, 0m);
        ShippingAddress address = new("A B", "1 Main St", null, "Town", null, "12345", "US");
        Order order = Order.Place(id, "ORD-2026-000001", Guid.NewGuid(), "key-1", totals, "USD", "standard", "Standard", address, [item], Now);
        order.CancelForExpiry(Now.AddMinutes(16));
        List<TimelineRow> rows = order.History.Select(h => new TimelineRow(h.FromStatus, h.ToStatus, h.CreatedAt, h.Reason)).ToList();

        OrderStatusNote? note = OrderStatusNotes.For(order.Status, rows);

        Assert.NotNull(note);
        Assert.Equal("Payment not completed in time", note.PillNote);
        Assert.NotNull(note.Alert);
        Assert.Equal("This order was cancelled", note.Alert.Title);
        Assert.Equal(ExpiredMessage, note.Alert.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("something-else")]
    [InlineData("RESERVATION-EXPIRED")]
    public void Cancelled_with_another_or_no_reason_gives_the_generic_note_and_alert(string? reason)
    {
        OrderStatusNote? note = OrderStatusNotes.For(OrderStatus.Cancelled, Cancelled(reason));

        Assert.NotNull(note);
        Assert.Equal("Order cancelled", note.PillNote);
        Assert.NotNull(note.Alert);
        Assert.Equal("This order was cancelled", note.Alert.Title);
        Assert.Equal("This order was cancelled and will not ship.", note.Alert.Message);
    }

    [Fact]
    public void Refunded_gives_the_pill_note_and_no_alert()
    {
        OrderStatusNote? note = OrderStatusNotes.For(OrderStatus.Refunded, []);

        Assert.NotNull(note);
        Assert.Equal("Refunded in full to your original payment method", note.PillNote);
        Assert.Null(note.Alert);
    }

    [Theory]
    [InlineData("PendingPayment")]
    [InlineData("Paid")]
    [InlineData("Processing")]
    [InlineData("Shipped")]
    [InlineData("Delivered")]
    [InlineData("Returned")]
    public void Other_statuses_have_no_note(string status) =>
        Assert.Null(OrderStatusNotes.For(Enum.Parse<OrderStatus>(status), []));

    // P2.53: timeline entry descriptions.
    private const string ExpiryDescription =
        "Payment was not completed before the hold on your items ran out, so we released them and cancelled the order.";

    public static TheoryData<string> AllStatuses() => new(Enum.GetNames<OrderStatus>());

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void Every_status_has_a_non_empty_description(string status) =>
        Assert.False(string.IsNullOrWhiteSpace(OrderStatusNotes.Describe(Enum.Parse<OrderStatus>(status), null)));

    [Theory]
    [InlineData("PendingPayment", null, "We received your order and held your items for payment.")]
    [InlineData("Paid", null, "Your payment was confirmed.")]
    [InlineData("Processing", null, "We started preparing your order.")]
    [InlineData("Shipped", null, "Your order was handed to the carrier.")]
    [InlineData("Delivered", null, "Your order was delivered.")]
    [InlineData("Cancelled", "reservation-expired", ExpiryDescription)]
    [InlineData("Cancelled", null, "This order was cancelled and will not ship.")]
    [InlineData("Cancelled", "something-else", "This order was cancelled and will not ship.")]
    [InlineData("Cancelled", "RESERVATION-EXPIRED", "This order was cancelled and will not ship.")]
    [InlineData("Refunded", null, "We refunded the full amount to your original payment method.")]
    [InlineData("Returned", null, "Your return was refunded in full to your original payment method.")]
    public void Each_status_has_its_exact_description(string status, string? reason, string expected) =>
        Assert.Equal(expected, OrderStatusNotes.Describe(Enum.Parse<OrderStatus>(status), reason));

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void A_reason_is_never_echoed_into_the_description(string status)
    {
        string text = OrderStatusNotes.Describe(Enum.Parse<OrderStatus>(status), "Internal: VIP, skip fraud check");

        Assert.DoesNotContain("VIP", text);
        Assert.Equal(OrderStatusNotes.Describe(Enum.Parse<OrderStatus>(status), null), text);
    }

    [Fact]
    public void Expired_cancellation_from_the_real_domain_is_described_as_expiry()
    {
        Order order = NewOrder();
        order.CancelForExpiry(Now.AddMinutes(16));
        OrderStatusChange last = order.History.Last();

        Assert.Equal(ExpiryDescription, OrderStatusNotes.Describe(last.ToStatus, last.Reason));
    }

    [Fact]
    public void Refund_in_full_from_the_real_domain_is_described_without_its_stored_reason()
    {
        Order order = NewOrder();
        order.MarkPaid(Now.AddMinutes(1));
        order.RefundInFull(null, Now.AddMinutes(2));
        OrderStatusChange last = order.History.Last();

        string text = OrderStatusNotes.Describe(last.ToStatus, last.Reason);

        Assert.Equal("We refunded the full amount to your original payment method.", text);
        Assert.DoesNotContain("Refunded in full", text);
    }

    private static Order NewOrder()
    {
        Guid id = Guid.NewGuid();
        OrderItem item = OrderItem.Create(Guid.NewGuid(), id, Guid.NewGuid(), "TEE-1", "Tee", 10.00m, 2);
        OrderTotals totals = OrderTotals.Compute([item.LineTotal], 5.00m, 0m);
        ShippingAddress address = new("A B", "1 Main St", null, "Town", null, "12345", "US");
        return Order.Place(id, "ORD-2026-000001", Guid.NewGuid(), "key-1", totals, "USD", "standard", "Standard", address, [item], Now);
    }
}
