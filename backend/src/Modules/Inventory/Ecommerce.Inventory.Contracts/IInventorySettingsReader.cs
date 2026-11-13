namespace Ecommerce.Inventory.Contracts;

/// <summary>Inventory's validated, read-only configuration for other modules (the Admin settings page).</summary>
public interface IInventorySettingsReader
{
    int LowStockThreshold { get; }
}
