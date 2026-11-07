using System.Reflection;
using Ecommerce.Orders.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Orders.Tests.Domain;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Admin = Guid.NewGuid();

    // There is no production factory (checkout is Slice 4), so the test builds the aggregate by reflection.
    private static Order OrderIn(OrderStatus status)
    {
        Order order = (Order)Activator.CreateInstance(typeof(Order), nonPublic: true)!;
        typeof(Order).GetProperty(nameof(Order.Id))!.SetValue(order, Guid.NewGuid());
        typeof(Order).GetProperty(nameof(Order.Status))!.SetValue(order, status);
        return order;
    }

    [Fact]
    public void Process_from_paid_moves_to_processing_with_one_history_row_and_a_version_bump()
    {
        Order order = OrderIn(OrderStatus.Paid);

        order.Process(Admin, Now);

        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Equal(1, order.Version);
        Assert.Equal(Now, order.UpdatedAt);
        OrderStatusChange change = Assert.Single(order.History);
        Assert.Equal((OrderStatus.Paid, OrderStatus.Processing, Admin), (change.FromStatus, change.ToStatus, change.ChangedBy));
    }

    [Theory]
    [InlineData("PendingPayment")]
    [InlineData("Processing")]
    [InlineData("Shipped")]
    [InlineData("Cancelled")]
    public void A_rejected_process_changes_nothing(string statusName)
    {
        OrderStatus status = Enum.Parse<OrderStatus>(statusName);
        Order order = OrderIn(status);

        Assert.Throws<DomainException>(() => order.Process(Admin, Now));

        Assert.Equal(status, order.Status);
        Assert.Equal(0, order.Version);
        Assert.Empty(order.History);
    }

    [Fact]
    public void Ship_adds_one_shipment_and_deliver_stamps_it()
    {
        Order order = OrderIn(OrderStatus.Processing);

        order.Ship(Admin, "example-post", "TRK-1", Now);
        Shipment shipment = Assert.Single(order.Shipments);
        Assert.Equal(("example-post", "TRK-1", Now, (DateTimeOffset?)null), (shipment.Carrier, shipment.TrackingNumber, shipment.ShippedAt, shipment.DeliveredAt));

        order.Deliver(Admin, Now.AddDays(2));
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(Now.AddDays(2), shipment.DeliveredAt);
        Assert.Equal(2, order.History.Count);
    }

    [Fact]
    public void Ship_from_paid_adds_no_shipment()
    {
        Order order = OrderIn(OrderStatus.Paid);

        Assert.Throws<DomainException>(() => order.Ship(Admin, "example-post", "TRK-1", Now));

        Assert.Empty(order.Shipments);
        Assert.Empty(order.History);
    }

    [Theory]
    [InlineData("Paid", "Paid")]
    [InlineData("Paid", "Cancelled")]
    [InlineData("Paid", "Shipped")]
    [InlineData("PendingPayment", "Paid")]
    [InlineData("Processing", "Refunded")]
    public void Override_refuses_every_edge_but_the_two_allowed_ones(string fromName, string toName)
    {
        OrderStatus from = Enum.Parse<OrderStatus>(fromName);
        OrderStatus to = Enum.Parse<OrderStatus>(toName);
        Order order = OrderIn(from);

        Assert.Throws<DomainException>(() => order.Override(Admin, to, "because", Now));

        Assert.Equal(from, order.Status);
        Assert.Empty(order.History);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Override_needs_a_reason(string reason)
    {
        Order order = OrderIn(OrderStatus.Paid);

        Assert.Throws<DomainException>(() => order.Override(Admin, OrderStatus.Processing, reason, Now));
        Assert.Empty(order.History);
    }

    [Fact]
    public void Override_records_the_reason_on_the_history_row()
    {
        Order order = OrderIn(OrderStatus.Paid);

        order.Override(Admin, OrderStatus.Processing, "picked early", Now);

        Assert.Equal("picked early", Assert.Single(order.History).Reason);
    }
}
