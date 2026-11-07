namespace Ecommerce.Orders.Domain;

/// <summary>The order totals (arch 7.4), computed once, server-side, from the line totals.</summary>
internal sealed record OrderTotals(decimal Subtotal, decimal DiscountTotal, decimal ShippingTotal, decimal TaxTotal)
{
    public decimal GrandTotal => Subtotal - DiscountTotal + ShippingTotal + TaxTotal;

    /// <summary>Tax is one flat rate on goods, rounded once at order level, half away from zero. Shipping is not taxed; there are no discounts in v1.</summary>
    public static OrderTotals Compute(IEnumerable<decimal> lineTotals, decimal shippingTotal, decimal taxRatePercent)
    {
        decimal subtotal = lineTotals.Sum();
        decimal tax = Math.Round(subtotal * taxRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
        return new OrderTotals(subtotal, 0m, shippingTotal, tax);
    }
}
