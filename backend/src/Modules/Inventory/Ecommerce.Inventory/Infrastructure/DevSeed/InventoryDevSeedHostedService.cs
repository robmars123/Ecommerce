using Ecommerce.Catalog.Contracts;
using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Infrastructure.DevSeed;

/// <summary>
/// Development stock (D13). Registered only inside Program.cs's Development-only dev-seed block, after the catalog seed.
/// Runs only while inventory_stock is empty. Every 8th product (by catalog order) gets no stock rows at all, so the shop list shows Out; the other variants cycle 25, 25, 25, 25, 3, none.
/// Goes through the admin adjust service, so every row is audited.
/// </summary>
internal sealed class InventoryDevSeedHostedService(
    IServiceScopeFactory scopes,
    ILogger<InventoryDevSeedHostedService> logger) : IHostedService
{
    private const int PageSize = 100;
    private const int NoStockEvery = 8;
    private const string Reason = "Development seed";
    private static readonly int?[] Pattern = [25, 25, 25, 25, 3, null];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopes.CreateScope();
            EcommerceDbContext db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();

            if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                logger.LogWarning("Inventory dev seed skipped: migrations are pending. Run dotnet-ef database update.");
                return;
            }
            if (await db.Set<InventoryStock>().AnyAsync(cancellationToken))
            {
                return;
            }

            IProductReader products = scope.ServiceProvider.GetRequiredService<IProductReader>();
            InventoryAdminService admin = scope.ServiceProvider.GetRequiredService<InventoryAdminService>();
            int index = 0;
            int productIndex = -1;
            Guid? currentProduct = null;
            for (int page = 1; ; page++)
            {
                VariantRefPage variants = await products.ListVariantsAsync(page, PageSize, cancellationToken);
                foreach (VariantRef variant in variants.Items)
                {
                    if (variant.ProductId != currentProduct)
                    {
                        currentProduct = variant.ProductId;
                        productIndex++;
                    }
                    if (productIndex % NoStockEvery == NoStockEvery - 1)
                    {
                        continue;
                    }
                    int? quantity = Pattern[index++ % Pattern.Length];
                    if (quantity is not null)
                    {
                        await admin.AdjustAsync(null, variant.VariantId, quantity.Value, 0, Reason, cancellationToken);
                    }
                }
                if (page * PageSize >= variants.TotalCount)
                {
                    break;
                }
            }
            logger.LogInformation("Inventory dev seed complete.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Inventory dev seed failed; the API continues without stock data.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
