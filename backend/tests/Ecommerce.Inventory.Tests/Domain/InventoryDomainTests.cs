using Ecommerce.Inventory.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Inventory.Tests.Domain;

public class InventoryDomainTests
{
    private static InventoryStock Stock(int onHand, int reserved = 0)
    {
        InventoryStock s = InventoryStock.Create(Guid.NewGuid());
        s.Adjust(onHand);
        if (reserved > 0)
        {
            s.Reserve(reserved);
        }
        return s;
    }

    [Fact]
    public void Reserve_up_to_available_works_and_moves_available()
    {
        InventoryStock s = Stock(5, 2);

        s.Reserve(3);

        Assert.Equal((5, 5, 0), (s.QuantityOnHand, s.QuantityReserved, s.Available));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reserve_more_than_available_or_a_non_positive_quantity_throws_and_changes_nothing(int quantity)
    {
        InventoryStock s = Stock(5, 2);

        Assert.Throws<DomainException>(() => s.Reserve(quantity));

        Assert.Equal((5, 2), (s.QuantityOnHand, s.QuantityReserved));
    }

    [Fact]
    public void Release_gives_reserved_back_but_never_more_than_is_reserved()
    {
        InventoryStock s = Stock(5, 2);

        s.Release(2);
        Assert.Equal(0, s.QuantityReserved);

        Assert.Throws<DomainException>(() => s.Release(1));
        Assert.Throws<DomainException>(() => Stock(5, 2).Release(3));
        Assert.Throws<DomainException>(() => Stock(5, 2).Release(0));
    }

    [Fact]
    public void Adjust_down_to_exactly_reserved_works_and_below_reserved_throws()
    {
        InventoryStock s = Stock(5, 3);

        s.Adjust(-2);
        Assert.Equal(3, s.QuantityOnHand);

        Assert.Throws<DomainException>(() => s.Adjust(-1));
        Assert.Equal(3, s.QuantityOnHand);
    }

    [Fact]
    public void A_new_reservation_is_Active_and_Expire_works_only_from_Active()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        InventoryReservation r = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, now.AddMinutes(15), now);
        Assert.Equal(ReservationStatus.Active, r.Status);

        r.Expire();
        Assert.Equal(ReservationStatus.Expired, r.Status);

        Assert.Throws<DomainException>(r.Expire);
    }
}
