using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Catalog.Application;

internal static class StockStatusRollup
{
    /// <summary>
    /// Product-level indicator for a list card: InStock if any known variant is in stock, else Low if any is low,
    /// else Out if any is out, else Unknown. Unknown variants are ignored.
    /// </summary>
    public static StockStatus Rollup(IEnumerable<StockStatus> variantStatuses)
    {
        List<StockStatus> known = variantStatuses.Where(s => s != StockStatus.Unknown).ToList();
        if (known.Contains(StockStatus.InStock)) return StockStatus.InStock;
        if (known.Contains(StockStatus.Low)) return StockStatus.Low;
        if (known.Contains(StockStatus.Out)) return StockStatus.Out;
        return StockStatus.Unknown;
    }
}
