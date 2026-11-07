using Ecommerce.Inventory.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Inventory.Tests.Domain;

/// <summary>Commit on Paid (arch 8.2 rule 3): on_hand and reserved fall together; a reservation commits once and never after it ended.</summary>
public class InventoryCommitTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static InventoryStock Stock(int onHand, int reserved)
    {
        InventoryStock s = InventoryStock.Create(Guid.NewGuid());
        s.Adjust(onHand);
        s.Reserve(reserved);
        return s;
    }

    private static InventoryReservation Reservation() =>
        InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, Now.AddMinutes(15), Now);

    [Fact]
    public void Stock_commit_lowers_on_hand_and_reserved_by_the_quantity()
    {
        InventoryStock s = Stock(5, 3);

        s.Commit(2);

        Assert.Equal((3, 1, 2), (s.QuantityOnHand, s.QuantityReserved, s.Available));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    public void Stock_commit_of_a_non_positive_quantity_or_more_than_reserved_throws_and_changes_nothing(int quantity)
    {
        InventoryStock s = Stock(5, 3);

        Assert.Throws<DomainException>(() => s.Commit(quantity));

        Assert.Equal((5, 3), (s.QuantityOnHand, s.QuantityReserved));
    }

    [Fact]
    public void An_Active_reservation_commits_once()
    {
        InventoryReservation r = Reservation();

        r.Commit();

        Assert.Equal(ReservationStatus.Committed, r.Status);
        Assert.Throws<DomainException>(() => r.Commit());
    }

    [Fact]
    public void An_expired_reservation_never_commits()
    {
        InventoryReservation r = Reservation();
        r.Expire();

        Assert.Throws<DomainException>(() => r.Commit());

        Assert.Equal(ReservationStatus.Expired, r.Status);
    }
}
