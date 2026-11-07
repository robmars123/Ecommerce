using Ecommerce.Orders.Domain;

namespace Ecommerce.Orders.Tests.Domain;

/// <summary>Test 30 (domain half): every (from, to) pair is checked against the arch 7.3 diagram.</summary>
public class OrderStatusMachineTests
{
    // Transcribed from arch 7.3, not derived from the code under test.
    private static readonly HashSet<(OrderStatus, OrderStatus)> Legal =
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

    private static readonly HashSet<(OrderStatus, OrderStatus)> Overridable =
    [
        (OrderStatus.Paid, OrderStatus.Processing),
        (OrderStatus.Shipped, OrderStatus.Delivered),
    ];

    public static TheoryData<int, int> AllPairs()
    {
        TheoryData<int, int> data = new TheoryData<int, int>();
        foreach (OrderStatus from in Enum.GetValues<OrderStatus>())
        {
            foreach (OrderStatus to in Enum.GetValues<OrderStatus>())
            {
                data.Add((int)from, (int)to);
            }
        }
        return data;
    }

    [Fact]
    public void There_are_eight_statuses_so_sixty_four_pairs() =>
        Assert.Equal(64, Enum.GetValues<OrderStatus>().Length * Enum.GetValues<OrderStatus>().Length);

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void IsLegal_matches_the_diagram_for_every_pair(int fromValue, int toValue) =>
        Assert.Equal(Legal.Contains(((OrderStatus)fromValue, (OrderStatus)toValue)), OrderStatusMachine.IsLegal((OrderStatus)fromValue, (OrderStatus)toValue));

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void IsOverridable_is_only_paid_to_processing_and_shipped_to_delivered(int fromValue, int toValue) =>
        Assert.Equal(Overridable.Contains(((OrderStatus)fromValue, (OrderStatus)toValue)), OrderStatusMachine.IsOverridable((OrderStatus)fromValue, (OrderStatus)toValue));

    [Fact]
    public void Every_overridable_edge_is_legal() =>
        Assert.All(Overridable, e => Assert.True(OrderStatusMachine.IsLegal(e.Item1, e.Item2)));
}
