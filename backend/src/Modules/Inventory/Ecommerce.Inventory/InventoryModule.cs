using Ecommerce.Inventory.Contracts;
using Ecommerce.Inventory.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Slice 2 replaces this registration with the inventory_stock reader.
        services.AddSingleton<IInventoryReader, UnknownInventoryReader>();
        return services;
    }
}
