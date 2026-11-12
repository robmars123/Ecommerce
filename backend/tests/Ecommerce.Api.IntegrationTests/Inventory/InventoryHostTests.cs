using System.Security.Cryptography;
using Ecommerce.Catalog.Infrastructure.DevSeed;
using Ecommerce.Inventory.Infrastructure.DevSeed;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Ecommerce.Api.IntegrationTests.Inventory;

// Config validation, the admin bootstrap audit row, and the development stock seed.
public class InventoryHostTests(PostgresFixture postgres)
{
    private static string AllMessages(Exception? ex)
    {
        List<string> parts = [];
        for (; ex is not null; ex = ex.InnerException)
        {
            parts.Add(ex.Message);
        }
        return string.Join(" | ", parts);
    }

    [Theory]
    [InlineData("Inventory:LowStockThreshold", "0")]
    [InlineData("Inventory:LowStockThreshold", "-3")]
    [InlineData("Inventory:ReservationSweepSeconds", "0")]
    public void A_non_positive_inventory_setting_stops_the_host_from_starting(string key, string value)
    {
        using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable, settings: new Dictionary<string, string> { [key] = value });

        Exception? ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.Contains(key, AllMessages(ex));
    }

    [Fact]
    public async Task The_configured_settings_are_a_threshold_of_3_and_a_30_second_sweep()
    {
        await using Identity.IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        using IServiceScope scope = env.Factory.Services.CreateScope();
        Ecommerce.Inventory.Infrastructure.InventoryOptions options =
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Ecommerce.Inventory.Infrastructure.InventoryOptions>>().Value;

        Assert.Equal((3, 30), (options.LowStockThreshold, options.ReservationSweepSeconds));
    }

    private async Task<(string Cs, string Email, string Secret)> MigratedAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string cs = await postgres.CreateDatabaseAsync(ct);
        await using ApiFactory migrator = new ApiFactory(cs);
        using IServiceScope scope = migrator.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EcommerceDbContext>().Database.MigrateAsync(ct);
        return (cs, "root@example.test", Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)));
    }

    [Fact]
    public async Task The_first_bootstrap_start_writes_one_audit_row_without_an_email_and_a_second_start_writes_none()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (string cs, string email, string secret) = await MigratedAsync();
        Dictionary<string, string> settings = new()
        {
            ["Identity:Bootstrap:AdminEmail"] = email,
            ["Identity:Bootstrap:AdminPassword"] = secret,
        };

        for (int i = 0; i < 2; i++)
        {
            await using ApiFactory factory = new ApiFactory(cs, settings: settings);
            factory.CreateClient();
        }

        await using NpgsqlDataSource source = NpgsqlDataSource.Create(cs);
        await using NpgsqlCommand cmd = source.CreateCommand(
            "SELECT actor_id, entity_type, entity_id, before_json::text, after_json::text, reason, (SELECT id FROM users LIMIT 1) FROM audit_log WHERE action = 'identity.admin-bootstrap'");
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
        Assert.True(await reader.ReadAsync(ct));
        Assert.True(reader.IsDBNull(0));
        Assert.Equal("User", reader.GetString(1));
        Assert.Equal(reader.GetGuid(6), reader.GetGuid(2));
        Assert.True(reader.IsDBNull(3));
        Assert.Contains("Admin", reader.GetString(4));
        Assert.DoesNotContain("@", reader.GetString(4));
        Assert.True(reader.IsDBNull(5));
        Assert.False(await reader.ReadAsync(ct), "a second start must not add another row");
    }

    [Fact]
    public async Task The_dev_seed_creates_the_25_and_3_pattern_leaves_some_products_without_rows_with_audit_rows_and_a_second_run_adds_nothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Identity.IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        await new CatalogDevSeeder().SeedAsync(env.Db, env.Clock, ct);
        long variants = await InventoryEnv.CountAsync(env, "SELECT count(*) FROM product_variants");
        InventoryDevSeedHostedService seed = new(env.Factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<InventoryDevSeedHostedService>.Instance);

        await seed.StartAsync(ct);

        long rows = await InventoryEnv.CountAsync(env, "SELECT count(*) FROM inventory_stock");
        Assert.InRange(rows, 1, variants - 1);
        Assert.True(await InventoryEnv.CountAsync(env, "SELECT count(*) FROM inventory_stock WHERE quantity_on_hand = 3") > 0);
        Assert.True(await InventoryEnv.CountAsync(env, "SELECT count(*) FROM inventory_stock WHERE quantity_on_hand = 25") > 0);
        Assert.Equal(rows, await InventoryEnv.CountAsync(env, "SELECT count(*) FROM inventory_stock WHERE quantity_on_hand IN (3, 25)"));
        const string visibleWithoutRows =
            "SELECT count(*) FROM products p WHERE p.is_active AND NOT EXISTS " +
            "(SELECT 1 FROM product_variants v JOIN inventory_stock s ON s.product_variant_id = v.id WHERE v.product_id = p.id)";
        Assert.True(await InventoryEnv.CountAsync(env, visibleWithoutRows) >= 1, "at least one visible product must have no stock rows");
        Assert.Equal(rows, await InventoryEnv.CountAsync(env,
            "SELECT count(*) FROM audit_log WHERE action = 'inventory.adjust' AND reason = 'Development seed' AND actor_id IS NULL"));

        await seed.StartAsync(ct);

        Assert.Equal(rows, await InventoryEnv.CountAsync(env, "SELECT count(*) FROM inventory_stock"));
        Assert.Equal(rows, await InventoryEnv.CountAsync(env, "SELECT count(*) FROM audit_log WHERE action = 'inventory.adjust'"));
    }
}
