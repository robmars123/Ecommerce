using Ecommerce.SharedKernel.Domain;
using CartAggregate = Ecommerce.Cart.Domain.Cart;

namespace Ecommerce.Cart.Tests.Domain;

public class CartDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Variant = Guid.NewGuid();

    private static CartAggregate NewCart() => CartAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), Now);

    [Fact]
    public void Add_creates_the_line_then_sums_into_it_and_refreshes_the_display_price()
    {
        CartAggregate cart = NewCart();

        cart.Add(Variant, 2, 10m, "USD", Now);
        cart.Add(Variant, 3, 12m, "USD", Now.AddMinutes(1));

        Assert.Equal(5, cart.QuantityOf(Variant));
        Assert.Equal(12m, Assert.Single(cart.Items).DisplayUnitPrice);
    }

    [Fact]
    public void Set_replaces_the_quantity_and_creates_a_missing_line()
    {
        CartAggregate cart = NewCart();

        cart.Set(Variant, 4, 10m, "USD", Now);
        cart.Set(Variant, 2, 10m, "USD", Now);

        Assert.Equal(2, cart.QuantityOf(Variant));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Add_and_Set_refuse_a_quantity_of_zero_or_less_and_change_nothing(int quantity)
    {
        CartAggregate cart = NewCart();
        cart.Add(Variant, 2, 10m, "USD", Now);

        Assert.Throws<DomainException>(() => cart.Add(Variant, quantity, 10m, "USD", Now));
        Assert.Throws<DomainException>(() => cart.Set(Variant, quantity, 10m, "USD", Now));

        Assert.Equal(2, cart.QuantityOf(Variant));
    }

    [Fact]
    public void Remove_deletes_the_line_and_reports_whether_it_was_there()
    {
        CartAggregate cart = NewCart();
        cart.Add(Variant, 2, 10m, "USD", Now);

        Assert.True(cart.Remove(Variant, Now));
        Assert.False(cart.Remove(Variant, Now));
        Assert.Equal(0, cart.QuantityOf(Variant));
        Assert.Empty(cart.Items);
    }
}
