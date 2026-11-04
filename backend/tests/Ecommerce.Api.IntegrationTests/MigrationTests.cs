using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests;

public class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrations_apply_cleanly_to_an_empty_postgres_17_database()
    {
        var ct = TestContext.Current.CancellationToken;
        var cs = await postgres.CreateDatabaseAsync(ct);
        await using var factory = new ApiFactory(cs);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
        await db.Database.MigrateAsync(ct);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(ct));
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(ct), m => m.EndsWith("_InitialBaseline", StringComparison.Ordinal));

        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(ct), m => m.EndsWith("_AddCatalog", StringComparison.Ordinal));

        var version = await db.Database.SqlQueryRaw<string>("SHOW server_version").ToListAsync(ct);
        Assert.StartsWith("17.", Assert.Single(version));
    }
}
