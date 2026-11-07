using Ecommerce.Inventory.Api;
using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Contracts;
using Ecommerce.Inventory.Infrastructure;
using Ecommerce.Inventory.Infrastructure.DevSeed;
using Ecommerce.Inventory.Infrastructure.Persistence;
using Ecommerce.Persistence;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Inventory;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleDbModel, InventoryDbModel>());

        services.AddOptions<InventoryOptions>()
            .Configure<IConfiguration>((o, c) =>
            {
                o.LowStockThreshold = c.GetValue("Inventory:LowStockThreshold", o.LowStockThreshold);
                o.ReservationSweepSeconds = c.GetValue("Inventory:ReservationSweepSeconds", o.ReservationSweepSeconds);
            })
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<InventoryOptions>, InventoryOptionsValidator>());

        services.AddSingleton(sp => new StockSettings(sp.GetRequiredService<IOptions<InventoryOptions>>().Value.LowStockThreshold));
        services.AddScoped<IInventoryStore, InventoryStore>();
        services.AddScoped<IInventoryReserver, InventoryReserver>();
        services.AddScoped<IInventoryReader, InventoryStockReader>();
        services.AddScoped<InventoryAdminService>();
        services.AddScoped<ReservationExpiryService>();
        services.AddHostedService<ReservationExpiryWorker>();
        services.AddScoped<IValidator<AdjustStockRequest>, AdjustStockRequestValidator>();
        return services;
    }

    /// <summary>Registers the development stock seed. The host decides whether the environment allows it.</summary>
    public static IServiceCollection AddInventoryDevSeed(this IServiceCollection services)
    {
        services.AddHostedService<InventoryDevSeedHostedService>();
        return services;
    }
}
