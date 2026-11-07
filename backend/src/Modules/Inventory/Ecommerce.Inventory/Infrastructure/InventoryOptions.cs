using Microsoft.Extensions.Options;

namespace Ecommerce.Inventory.Infrastructure;

internal sealed class InventoryOptions
{
    public int LowStockThreshold { get; set; } = 5;
    public int ReservationSweepSeconds { get; set; } = 30;
}

internal sealed class InventoryOptionsValidator : IValidateOptions<InventoryOptions>
{
    public ValidateOptionsResult Validate(string? name, InventoryOptions options)
    {
        List<string> failures = [];
        if (options.LowStockThreshold < 1)
        {
            failures.Add("Inventory:LowStockThreshold must be 1 or greater.");
        }
        if (options.ReservationSweepSeconds < 1)
        {
            failures.Add("Inventory:ReservationSweepSeconds must be 1 or greater.");
        }
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
