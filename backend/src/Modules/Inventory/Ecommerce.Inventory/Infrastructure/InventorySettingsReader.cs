using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Inventory.Infrastructure;

internal sealed class InventorySettingsReader(StockSettings settings) : IInventorySettingsReader
{
    public int LowStockThreshold => settings.LowStockThreshold;
}
