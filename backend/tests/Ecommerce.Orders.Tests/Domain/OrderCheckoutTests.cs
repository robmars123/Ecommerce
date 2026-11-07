using Ecommerce.Orders.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Orders.Tests.Domain;

/// <summary>Place, MarkPaid and CancelForExpiry (arch 7.3, 8.2): legal edges only, one history row each, actor null for system moves.</summary>
public class OrderCheckoutTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Buyer = Guid.NewGuid();

    private static Order Placed()
    {
        Guid id = Guid.NewGuid();
        OrderItem item = OrderItem.Create(Guid.NewGuid(), id, Guid.NewGuid(), "TEE-1", "Tee", 10.00m, 2);
        OrderTotals totals = OrderTotals.Compute([item.LineTotal], 5.00m, 0m);
        ShippingAddress address = new("A B", "1 Main St", null, "Town", null, "12345", "US");
        return Order.Place(id, "ORD-2026-000001", Buyer, "key-1", totals, "USD", "standard", "Standard", address, [item], Now);
    }

    private static Order In(OrderStatus status)
    {
        Order order = Placed();
        typeof(Order).GetProperty(nameof(Order.Status))!.SetValue(order, status);
        return order;
    }

    [Fact]
    public void Place_creates_a_PendingPayment_order_with_totals_items_and_one_history_row()
    {
        Order order = Placed();

        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(0, order.Version);
        Assert.Equal("key-1", order.IdempotencyKey);
        Assert.Equal((20.00m, 0m, 5.00m, 0m, 25.00m), (order.Subtotal, order.DiscountTotal, order.ShippingTotal, order.TaxTotal, order.GrandTotal));
        OrderItem item = Assert.Single(order.Items);
        Assert.Equal((10.00m, 2, 20.00m), (item.UnitPriceSnapshot, item.Quantity, item.LineTotal));
        OrderStatusChange change = Assert.Single(order.History);
        Assert.Equal((null, OrderStatus.PendingPayment, (Guid?)Buyer), (change.FromStatus, change.ToStatus, change.ChangedBy));
    }

    [Fact]
    public void MarkPaid_moves_PendingPayment_to_Paid_with_actor_null_and_a_version_bump()
    {
        Order order = Placed();

        order.MarkPaid(Now.AddMinutes(1));

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(1, order.Version);
        OrderStatusChange change = order.History[^1];
        Assert.Equal((OrderStatus.PendingPayment, OrderStatus.Paid, (Guid?)null), (change.FromStatus, change.ToStatus, change.ChangedBy));
    }

    [Fact]
    public void CancelForExpiry_moves_PendingPayment_to_Cancelled_with_the_reason_and_actor_null()
    {
        Order order = Placed();

        order.CancelForExpiry(Now.AddMinutes(16));

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        OrderStatusChange change = order.History[^1];
        Assert.Equal((OrderStatus.Cancelled, (Guid?)null, "reservation-expired"), (change.ToStatus, change.ChangedBy, change.Reason));
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Processing")]
    [InlineData("Shipped")]
    [InlineData("Delivered")]
    [InlineData("Cancelled")]
    [InlineData("Refunded")]
    [InlineData("Returned")]
    public void MarkPaid_and_CancelForExpiry_are_rejected_from_every_other_status_and_change_nothing(string statusName)
    {
        OrderStatus status = Enum.Parse<OrderStatus>(statusName);
        Order paid = In(status);
        Order cancelled = In(status);

        Assert.Throws<DomainException>(() => paid.MarkPaid(Now));
        Assert.Throws<DomainException>(() => cancelled.CancelForExpiry(Now));

        Assert.Equal((status, 0, 1), (paid.Status, paid.Version, paid.History.Count));
        Assert.Equal((status, 0, 1), (cancelled.Status, cancelled.Version, cancelled.History.Count));
    }
}
