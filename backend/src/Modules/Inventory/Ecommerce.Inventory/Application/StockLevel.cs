using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Inventory.Application;

internal static class StockLevel
{
    /// <summary>Arch 7.4: available at or below 0 is Out; 1 to the threshold is Low; above it is InStock.</summary>
    public static StockStatus Classify(int available, int threshold) =>
        available <= 0 ? StockStatus.Out : available <= threshold ? StockStatus.Low : StockStatus.InStock;
}
