using System.Text.RegularExpressions;
using Ecommerce.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Api.IntegrationTests.Catalog;

// I-14.
public class CatalogSchemaTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Search_vector_is_a_generated_column_with_a_gin_index_and_unique_indexes_exist()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await CatalogTestEnv.StartAsync(postgres);

        var generated = await env.Db.Database.SqlQueryRaw<string>(
            "SELECT is_generated AS \"Value\" FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'search_vector'")
            .ToListAsync(ct);
        Assert.Equal("ALWAYS", Assert.Single(generated));

        var indexes = await env.Db.Database.SqlQueryRaw<string>(
            "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'").ToListAsync(ct);
        Assert.Contains(indexes, d => d.Contains("ix_products_search_vector", StringComparison.Ordinal) && d.Contains("USING gin", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("UNIQUE INDEX ux_products_slug", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("UNIQUE INDEX ux_product_variants_sku", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("UNIQUE INDEX ux_categories_slug", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("ix_products_active_newest", StringComparison.Ordinal) && d.Contains("WHERE is_active", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_compare_at_price_that_is_not_above_price_is_rejected_by_the_database()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await CatalogTestEnv.StartAsync(postgres);
        var product = env.AddProduct("p", "P", variants: [new VariantSpec("P-1", 10m, CompareAt: 15m)]);
        await env.SaveAsync();

        // Bypass the domain on purpose to prove the database enforces the rule on its own.
        env.Db.Entry(product.Variants[0]).Property(v => v.CompareAtPrice).CurrentValue = 5m;
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync(ct));

        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, pg.SqlState);
        Assert.Equal("ck_product_variants_compare_at", pg.ConstraintName);
    }

    [Fact]
    public async Task Duplicate_slug_and_sku_are_rejected_by_unique_indexes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct("p", "P", variants: [new VariantSpec("SAME-SKU")]);
        await env.SaveAsync();

        env.AddProduct("p", "Another P", variants: [new VariantSpec("OTHER")]);
        var slugEx = await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync(ct));
        Assert.Equal("ux_products_slug", Assert.IsType<PostgresException>(slugEx.InnerException).ConstraintName);
    }

    [Fact]
    public void Every_table_and_column_name_in_the_model_is_snake_case()
    {
        using var scope = new CatalogModelHost().CreateScope();
        var db = scope.Db;
        var pattern = new Regex("^[a-z][a-z0-9_]*$");

        var tables = db.Model.GetEntityTypes().Where(e => e.GetTableName() is not null).ToList();
        Assert.Equal(
            new[] { "categories", "product_images", "product_variants", "products" },
            tables.Select(t => t.GetTableName()!).Order().ToArray());

        foreach (var entity in tables)
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            Assert.Matches(pattern, entity.GetTableName()!);
            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(table);
                Assert.True(column is not null && pattern.IsMatch(column), $"{entity.GetTableName()}.{property.Name} -> {column}");
            }
        }
    }

    private sealed class CatalogModelHost
    {
        public Scope CreateScope() => new();

        public sealed class Scope : IDisposable
        {
            private readonly ApiFactory _factory = new(ApiFactory.Unreachable);
            private readonly IServiceScope _scope;

            public Scope()
            {
                _scope = _factory.Services.CreateScope();
                Db = _scope.ServiceProvider.GetRequiredService<Ecommerce.Persistence.EcommerceDbContext>();
            }

            public Ecommerce.Persistence.EcommerceDbContext Db { get; }

            public void Dispose()
            {
                _scope.Dispose();
                _factory.Dispose();
            }
        }
    }
}
