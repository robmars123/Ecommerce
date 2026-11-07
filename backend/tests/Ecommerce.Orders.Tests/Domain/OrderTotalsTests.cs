using Ecommerce.Orders.Domain;

namespace Ecommerce.Orders.Tests.Domain;

/// <summary>Arch 7.4 totals: tax rounded once at order level, half away from zero. Test 23 (domain half).</summary>
public class OrderTotalsTests
{
    [Fact]
    public void Tax_rounds_half_away_from_zero_not_bankers()
    {
        OrderTotals totals = OrderTotals.Compute([12.05m], 5.00m, 10m);

        // 12.05 * 10 / 100 = 1.205: away from zero gives 1.21, banker rounding would give 1.20.
        Assert.Equal(1.21m, totals.TaxTotal);
        Assert.Equal((12.05m, 0m, 5.00m), (totals.Subtotal, totals.DiscountTotal, totals.ShippingTotal));
        Assert.Equal(18.26m, totals.GrandTotal);
    }

    [Fact]
    public void Tax_is_rounded_once_on_the_order_never_per_line()
    {
        // Each line alone is 0.004 (rounds to 0.00); the order is 0.012 (rounds to 0.01).
        OrderTotals totals = OrderTotals.Compute([0.04m, 0.04m, 0.04m], 0m, 10m);

        Assert.Equal(0.01m, totals.TaxTotal);
    }

    [Fact]
    public void Shipping_is_not_taxed_and_a_zero_rate_adds_no_tax()
    {
        OrderTotals taxed = OrderTotals.Compute([100.00m], 20.00m, 8m);
        OrderTotals untaxed = OrderTotals.Compute([100.00m], 20.00m, 0m);

        Assert.Equal(8.00m, taxed.TaxTotal);
        Assert.Equal(128.00m, taxed.GrandTotal);
        Assert.Equal(0m, untaxed.TaxTotal);
        Assert.Equal(120.00m, untaxed.GrandTotal);
    }

    [Fact]
    public void The_half_cent_case_rounds_up()
    {
        OrderTotals totals = OrderTotals.Compute([0.05m], 0m, 10m);

        Assert.Equal(0.01m, totals.TaxTotal);
    }
}
