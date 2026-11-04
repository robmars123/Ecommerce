using System.Net;
using Ecommerce.Inventory.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ecommerce.Api.IntegrationTests.Catalog;

// I-10 and I-11.
public class CatalogStockAndImageTests(PostgresFixture postgres)
{
    private sealed class FakeInventoryReader : IInventoryReader
    {
        public Dictionary<Guid, StockStatus> Statuses { get; } = [];
        public List<IReadOnlyCollection<Guid>> Calls { get; } = [];

        public Task<IReadOnlyDictionary<Guid, StockStatus>> GetStockStatusesAsync(
            IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
        {
            Calls.Add(variantIds.ToList());
            var result = Statuses.Where(kv => variantIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            return Task.FromResult<IReadOnlyDictionary<Guid, StockStatus>>(result);
        }
    }

    [Fact]
    public async Task With_the_default_registration_every_stock_status_is_Unknown()
    {
        await using var env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct("p", "P", variants: [new VariantSpec("P-1"), new VariantSpec("P-2", 12m)]);
        await env.SaveAsync();

        var (_, list) = await env.GetAsync("/api/v1/products");
        var (_, detail) = await env.GetAsync("/api/v1/products/p");

        Assert.Equal("Unknown", list.RootElement.GetProperty("items")[0].GetProperty("stockStatus").GetString());
        Assert.All(detail.RootElement.GetProperty("variants").EnumerateArray(), v => Assert.Equal("Unknown", v.GetProperty("stockStatus").GetString()));
    }

    [Fact]
    public async Task A_fake_reader_drives_per_variant_status_on_detail_and_the_rollup_on_the_list_with_one_call_per_request()
    {
        var fake = new FakeInventoryReader();
        await using var env = await CatalogTestEnv.StartAsync(postgres, services =>
        {
            services.RemoveAll<IInventoryReader>();
            services.AddSingleton<IInventoryReader>(fake);
        });
        var mixed = env.AddProduct("mixed", "Mixed", variants: [new VariantSpec("M-1", 10m), new VariantSpec("M-2", 11m)]);
        var gone = env.AddProduct("gone", "Gone", variants: [new VariantSpec("G-1")]);
        var low = env.AddProduct("low", "Low", variants: [new VariantSpec("L-1")]);
        await env.SaveAsync();
        fake.Statuses[mixed.Variants.Single(v => v.Sku == "M-1").Id] = StockStatus.Out;
        fake.Statuses[mixed.Variants.Single(v => v.Sku == "M-2").Id] = StockStatus.InStock;
        fake.Statuses[gone.Variants[0].Id] = StockStatus.Out;
        fake.Statuses[low.Variants[0].Id] = StockStatus.Low;

        var (status, list) = await env.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.OK, status);
        var byslug = list.RootElement.GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("slug").GetString()!, i => i.GetProperty("stockStatus").GetString());
        Assert.Equal("InStock", byslug["mixed"]);
        Assert.Equal("Out", byslug["gone"]);
        Assert.Equal("Low", byslug["low"]);
        var listCall = Assert.Single(fake.Calls);
        Assert.Equal(4, listCall.Count);

        fake.Calls.Clear();
        var (_, detail) = await env.GetAsync("/api/v1/products/mixed");

        var statuses = detail.RootElement.GetProperty("variants").EnumerateArray()
            .ToDictionary(v => v.GetProperty("sku").GetString()!, v => v.GetProperty("stockStatus").GetString());
        Assert.Equal("Out", statuses["M-1"]);
        Assert.Equal("InStock", statuses["M-2"]);
        Assert.Equal(2, Assert.Single(fake.Calls).Count);
    }

    [Fact]
    public async Task Image_urls_are_built_from_the_configured_base_with_each_segment_escaped()
    {
        await using var env = await CatalogTestEnv.StartAsync(
            postgres,
            settings: new Dictionary<string, string> { ["Catalog:Images:BaseUrl"] = "https://img.test.invalid/base/" });
        env.AddProduct("p", "P", images: [new ImageSpec("dev/a b.jpg", "first", 1), new ImageSpec("dev/z.jpg", "second", 2)]);
        await env.SaveAsync();

        var (_, list) = await env.GetAsync("/api/v1/products");
        var (_, detail) = await env.GetAsync("/api/v1/products/p");

        Assert.Equal("https://img.test.invalid/base/dev/a%20b.jpg", list.RootElement.GetProperty("items")[0].GetProperty("primaryImage").GetProperty("url").GetString());
        Assert.Equal("first", list.RootElement.GetProperty("items")[0].GetProperty("primaryImage").GetProperty("altText").GetString());
        Assert.Equal("https://img.test.invalid/base/dev/z.jpg", detail.RootElement.GetProperty("images")[1].GetProperty("url").GetString());
    }

    [Fact]
    public async Task Image_url_is_null_when_no_base_url_is_configured_and_primary_image_is_null_without_images()
    {
        await using var env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct("with-image", "With Image", images: [new ImageSpec("dev/a.jpg", "alt", 1)]);
        env.AddProduct("without-image", "Without Image");
        await env.SaveAsync();

        var (_, list) = await env.GetAsync("/api/v1/products?sort=price-asc");

        var items = list.RootElement.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("slug").GetString()!);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, items["with-image"].GetProperty("primaryImage").GetProperty("url").ValueKind);
        Assert.Equal("alt", items["with-image"].GetProperty("primaryImage").GetProperty("altText").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, items["without-image"].GetProperty("primaryImage").ValueKind);
    }

    [Fact]
    public void A_non_http_image_base_url_fails_startup()
    {
        using var factory = new ApiFactory(
            ApiFactory.Unreachable,
            settings: new Dictionary<string, string> { ["Catalog:Images:BaseUrl"] = "ftp://img.test.invalid" });

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.Contains("Catalog:Images:BaseUrl", FlattenMessages(ex));
    }

    private static string FlattenMessages(Exception? ex)
    {
        var parts = new List<string>();
        for (; ex is not null; ex = ex.InnerException)
        {
            parts.Add(ex.Message);
        }
        return string.Join(" | ", parts);
    }
}
