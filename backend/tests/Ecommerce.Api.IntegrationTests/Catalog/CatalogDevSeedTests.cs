using Ecommerce.Catalog.Infrastructure.DevSeed;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Api.IntegrationTests.Catalog;

// I-15.
public class CatalogDevSeedTests(PostgresFixture postgres)
{
    private static readonly string[] HiddenSlugs = ["retired-scarf", "draft-vase", "hidden-apron", "ghost-hat"];

    [Fact]
    public async Task Seeding_twice_is_idempotent_and_hidden_products_are_not_listed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await CatalogTestEnv.StartAsync(postgres);
        var clock = new FakeClock(CatalogTestEnv.Epoch);
        var seeder = new CatalogDevSeeder();

        await seeder.SeedAsync(env.Db, clock, ct);
        var first = await Counts(env);
        await seeder.SeedAsync(env.Db, clock, ct);
        var second = await Counts(env);

        Assert.Equal(first, second);
        Assert.True(first.Products >= 24 && first.Categories == 6 && first.Variants > first.Products && first.Images >= first.Products, first.ToString());

        var (_, page1) = await env.GetAsync("/api/v1/products?pageSize=100");
        var slugs = CatalogTestEnv.ItemSlugs(page1);
        Assert.Empty(slugs.Intersect(HiddenSlugs));
        Assert.Equal(first.Products - HiddenSlugs.Length, slugs.Length);
    }

    [Fact]
    public async Task Reseeding_never_republishes_or_touches_an_existing_product()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await CatalogTestEnv.StartAsync(postgres);
        var clock = new FakeClock(CatalogTestEnv.Epoch);
        var seeder = new CatalogDevSeeder();
        await seeder.SeedAsync(env.Db, clock, ct);

        var product = await env.Db.Set<Ecommerce.Catalog.Domain.Product>().FirstAsync(p => p.IsActive, ct);
        var slug = product.Slug;
        product.Unpublish(CatalogTestEnv.Epoch.AddHours(1));
        await env.Db.SaveChangesAsync(ct);
        var updatedAtBefore = product.UpdatedAt;
        env.Db.ChangeTracker.Clear();

        clock.UtcNow = CatalogTestEnv.Epoch.AddDays(1);
        await seeder.SeedAsync(env.Db, clock, ct);

        env.Db.ChangeTracker.Clear();
        var after = await env.Db.Set<Ecommerce.Catalog.Domain.Product>().AsNoTracking().SingleAsync(p => p.Slug == slug, ct);
        Assert.False(after.IsActive);
        Assert.Equal(updatedAtBefore, after.UpdatedAt);
    }

    [Fact]
    public async Task Enabling_the_seeder_outside_Development_fails_startup_naming_the_setting()
    {
        using var factory = new ApiFactory(
            ApiFactory.Unreachable,
            settings: new Dictionary<string, string> { ["Catalog:DevSeed:Enabled"] = "true" });

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        var messages = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException)
        {
            messages.Add(e.Message);
        }
        Assert.Contains("Catalog:DevSeed:Enabled", string.Join(" | ", messages));
        await Task.CompletedTask;
    }

    private static async Task<(int Categories, int Products, int Variants, int Images)> Counts(CatalogTestEnv env)
    {
        var ct = TestContext.Current.CancellationToken;
        env.Db.ChangeTracker.Clear();
        return (
            await env.Db.Set<Ecommerce.Catalog.Domain.Category>().CountAsync(ct),
            await env.Db.Set<Ecommerce.Catalog.Domain.Product>().CountAsync(ct),
            await env.Db.Set<Ecommerce.Catalog.Domain.ProductVariant>().CountAsync(ct),
            await env.Db.Set<Ecommerce.Catalog.Domain.ProductImage>().CountAsync(ct));
    }
}
