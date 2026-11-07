using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Catalog.Application;

internal static class StockStatusRollup
{
    /// <summary>
    /// Product-level indicator for a list card (arch 7.4): InStock if any variant is in stock, else Low if any is low,
    /// else Out (including no variants, or only Out and unknown ones). Never returns Unknown.
    /// </summary>
    public static StockStatus Rollup(IEnumerable<StockStatus> variantStatuses)
    {
        List<StockStatus> statuses = variantStatuses.ToList();
        if (statuses.Contains(StockStatus.InStock)) return StockStatus.InStock;
        if (statuses.Contains(StockStatus.Low)) return StockStatus.Low;
        return StockStatus.Out;
    }
}
