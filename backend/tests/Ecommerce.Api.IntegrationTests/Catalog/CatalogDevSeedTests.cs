using Ecommerce.Catalog.Domain;
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
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        FakeClock clock = new FakeClock(CatalogTestEnv.Epoch);
        CatalogDevSeeder seeder = new CatalogDevSeeder();

        await seeder.SeedAsync(env.Db, clock, ct);
        (int Categories, int Products, int Variants, int Images) first = await Counts(env);
        await seeder.SeedAsync(env.Db, clock, ct);
        (int Categories, int Products, int Variants, int Images) second = await Counts(env);

        Assert.Equal(first, second);
        Assert.True(first.Products >= 24 && first.Categories == 6 && first.Variants > first.Products && first.Images >= first.Products, first.ToString());

        (System.Net.HttpStatusCode _, System.Text.Json.JsonDocument? page1) = await env.GetAsync("/api/v1/products?pageSize=100");
        string[] slugs = CatalogTestEnv.ItemSlugs(page1);
        Assert.Empty(slugs.Intersect(HiddenSlugs));
        Assert.Equal(first.Products - HiddenSlugs.Length, slugs.Length);
    }

    [Fact]
    public async Task Reseeding_never_republishes_or_touches_an_existing_product()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        FakeClock clock = new FakeClock(CatalogTestEnv.Epoch);
        CatalogDevSeeder seeder = new CatalogDevSeeder();
        await seeder.SeedAsync(env.Db, clock, ct);

        Product product = await env.Db.Set<Ecommerce.Catalog.Domain.Product>().FirstAsync(p => p.IsActive, ct);
        string slug = product.Slug;
        product.Unpublish(CatalogTestEnv.Epoch.AddHours(1));
        await env.Db.SaveChangesAsync(ct);
        DateTimeOffset updatedAtBefore = product.UpdatedAt;
        env.Db.ChangeTracker.Clear();

        clock.UtcNow = CatalogTestEnv.Epoch.AddDays(1);
        await seeder.SeedAsync(env.Db, clock, ct);

        env.Db.ChangeTracker.Clear();
        Product after = await env.Db.Set<Ecommerce.Catalog.Domain.Product>().AsNoTracking().SingleAsync(p => p.Slug == slug, ct);
        Assert.False(after.IsActive);
        Assert.Equal(updatedAtBefore, after.UpdatedAt);
    }

    [Fact]
    public async Task Enabling_the_seeder_outside_Development_fails_startup_naming_the_setting()
    {
        using ApiFactory factory = new ApiFactory(
            ApiFactory.Unreachable,
            settings: new Dictionary<string, string> { ["Catalog:DevSeed:Enabled"] = "true" });

        Exception? ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        List<string> messages = new List<string>();
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            messages.Add(e.Message);
        }
        Assert.Contains("Catalog:DevSeed:Enabled", string.Join(" | ", messages));
        await Task.CompletedTask;
    }

    private static async Task<(int Categories, int Products, int Variants, int Images)> Counts(CatalogTestEnv env)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        env.Db.ChangeTracker.Clear();
        return (
            await env.Db.Set<Ecommerce.Catalog.Domain.Category>().CountAsync(ct),
            await env.Db.Set<Ecommerce.Catalog.Domain.Product>().CountAsync(ct),
            await env.Db.Set<Ecommerce.Catalog.Domain.ProductVariant>().CountAsync(ct),
            await env.Db.Set<Ecommerce.Catalog.Domain.ProductImage>().CountAsync(ct));
    }
}
