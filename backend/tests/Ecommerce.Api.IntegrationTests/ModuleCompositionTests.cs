using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests;

public class ModuleCompositionTests
{
    [Fact]
    public void Runtime_model_matches_the_migration_snapshot()
    {
        using var factory = new ApiFactory(ApiFactory.Unreachable);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Runtime_context_discovers_both_existing_migrations()
    {
        using var factory = new ApiFactory(ApiFactory.Unreachable);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();

        var migrations = db.Database.GetMigrations().ToList();

        Assert.Contains("20261103043930_InitialBaseline", migrations);
        Assert.Contains("20261103185341_AddCatalog", migrations);
    }
}
