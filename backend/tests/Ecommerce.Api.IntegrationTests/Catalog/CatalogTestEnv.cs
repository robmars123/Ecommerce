using System.Net;
using System.Text.Json;
using Ecommerce.SharedKernel.Time;
using Ecommerce.Catalog.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests.Catalog;

public sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

internal sealed record VariantSpec(
    string Sku,
    decimal Price = 10m,
    bool Active = true,
    string Currency = "USD",
    decimal? CompareAt = null,
    string? Name = null);

internal sealed record ImageSpec(string Key, string Alt = "", int SortOrder = 0);

/// <summary>
/// A fresh migrated database plus a real HTTP client. Fixtures are built through domain factories and
/// EcommerceDbContext (never the dev seeder, never raw SQL), so tests do not depend on seed content.
/// </summary>
internal sealed class CatalogTestEnv : IAsyncDisposable
{
    public static readonly DateTimeOffset Epoch = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IServiceScope _scope;
    private readonly Dictionary<string, Category> _categories = [];
    private readonly FakeClock _clock = new(Epoch);

    private CatalogTestEnv(ApiFactory factory, HttpClient client, IServiceScope scope, EcommerceDbContext db, string connectionString)
    {
        Factory = factory;
        Client = client;
        _scope = scope;
        Db = db;
        ConnectionString = connectionString;
    }

    public ApiFactory Factory { get; }
    public HttpClient Client { get; }
    public EcommerceDbContext Db { get; }
    public string ConnectionString { get; }

    public static async Task<CatalogTestEnv> StartAsync(
        PostgresFixture postgres,
        Action<IServiceCollection>? configureServices = null,
        IReadOnlyDictionary<string, string>? settings = null)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string cs = await postgres.CreateDatabaseAsync(ct);
        ApiFactory factory = new ApiFactory(cs, configureServices, settings);
        IServiceScope scope = factory.Services.CreateScope();
        EcommerceDbContext db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
        await db.Database.MigrateAsync(ct);
        return new CatalogTestEnv(factory, factory.CreateClient(), scope, db, cs);
    }

    public Category AddCategory(string slug, string name, string? parentSlug = null)
    {
        Category category = Category.Create(Guid.CreateVersion7(Epoch), slug, name, parentSlug is null ? null : _categories[parentSlug].Id);
        _categories[slug] = category;
        Db.Add(category);
        return category;
    }

    public Product AddProduct(
        string slug,
        string name,
        string category = "tops",
        string description = "",
        DateTimeOffset? createdAt = null,
        bool publish = true,
        IReadOnlyList<VariantSpec>? variants = null,
        IReadOnlyList<ImageSpec>? images = null)
    {
        if (!_categories.ContainsKey(category))
        {
            AddCategory(category, category);
        }

        DateTimeOffset at = createdAt ?? Epoch;
        Product product = Product.Create(Guid.CreateVersion7(at), slug, name, description, _categories[category].Id, at);
        List<ProductVariant> toDeactivate = new List<ProductVariant>();
        foreach (VariantSpec v in variants ?? [new VariantSpec(slug.ToUpperInvariant() + "-1")])
        {
            ProductVariant added = product.AddVariant(
                Guid.CreateVersion7(at), v.Sku, v.Name ?? v.Sku, null, null, v.Price, v.Currency, v.CompareAt, 100, at);
            if (!v.Active)
            {
                toDeactivate.Add(added);
            }
        }
        foreach (ImageSpec i in images ?? [])
        {
            product.AddImage(Guid.CreateVersion7(at), i.Key, i.Alt, i.SortOrder, at);
        }
        if (publish)
        {
            product.Publish(at);
        }
        toDeactivate.ForEach(v => v.Deactivate());
        Db.Add(product);
        return product;
    }

    public Task SaveAsync() => Db.SaveChangesAsync(TestContext.Current.CancellationToken);

    public async Task<(HttpStatusCode Status, JsonDocument Body)> GetAsync(string url)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Correlation-ID", "catalog-test-1");
        HttpResponseMessage response = await Client.SendAsync(request, ct);
        string text = await response.Content.ReadAsStringAsync(ct);
        return (response.StatusCode, JsonDocument.Parse(text.Length == 0 ? "{}" : text));
    }

    public static string[] ItemSlugs(JsonDocument body) =>
        body.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()!).ToArray();

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _scope.Dispose();
        await Factory.DisposeAsync();
    }
}
