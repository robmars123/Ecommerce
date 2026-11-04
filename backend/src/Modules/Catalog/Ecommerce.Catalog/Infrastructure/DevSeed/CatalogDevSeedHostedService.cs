using Ecommerce.SharedKernel.Time;
using Ecommerce.Catalog.Infrastructure.DevSeed;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Catalog.Infrastructure.DevSeed;

/// <summary>
/// Seeds development data at startup. Registered only when Catalog:DevSeed:Enabled is true, and Program.cs
/// refuses to start outside Development when it is. Skips (with a warning) if migrations are pending.
/// </summary>
internal sealed class CatalogDevSeedHostedService(
    IServiceScopeFactory scopes,
    ILogger<CatalogDevSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();

            var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pending.Any())
            {
                logger.LogWarning("Catalog dev seed skipped: migrations are pending. Run dotnet-ef database update.");
                return;
            }

            await scope.ServiceProvider.GetRequiredService<CatalogDevSeeder>()
                .SeedAsync(db, scope.ServiceProvider.GetRequiredService<IClock>(), cancellationToken);
            logger.LogInformation("Catalog dev seed complete.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Catalog dev seed failed; the API continues without seed data.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
