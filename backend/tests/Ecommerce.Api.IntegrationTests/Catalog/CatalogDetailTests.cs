using System.Net;
using System.Text.Json;

namespace Ecommerce.Api.IntegrationTests.Catalog;

// I-3 and I-12.
public class CatalogDetailTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Detail_returns_only_active_variants_ordered_by_price_then_sku_and_images_by_sort_order()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct(
            "blue-shirt", "Blue Shirt", category: "tops", description: "A shirt",
            variants:
            [
                new VariantSpec("BS-L", 24.5m, CompareAt: 30m, Name: "Large"),
                new VariantSpec("BS-B", 24.5m, Name: "Medium B"),
                new VariantSpec("BS-A", 20m, Name: "Small"),
                new VariantSpec("BS-X", 1m, Active: false, Name: "Hidden"),
            ],
            images: [new ImageSpec("blue/2.jpg", "back", 2), new ImageSpec("blue/1.jpg", "front", 1)]);
        await env.SaveAsync();

        (HttpStatusCode status, JsonDocument? body) = await env.GetAsync("/api/v1/products/blue-shirt");

        Assert.Equal(HttpStatusCode.OK, status);
        JsonElement root = body.RootElement;
        Assert.Equal("Blue Shirt", root.GetProperty("name").GetString());
        Assert.Equal("A shirt", root.GetProperty("description").GetString());
        Assert.Equal("tops", root.GetProperty("category").GetProperty("slug").GetString());

        JsonElement[] variants = root.GetProperty("variants").EnumerateArray().ToArray();
        Assert.Equal(new[] { "BS-A", "BS-B", "BS-L" }, variants.Select(v => v.GetProperty("sku").GetString()).ToArray());
        Assert.Equal(JsonValueKind.String, variants[0].GetProperty("price").GetProperty("amount").ValueKind);
        Assert.Equal("20.00", variants[0].GetProperty("price").GetProperty("amount").GetString());
        Assert.Equal("24.50", variants[1].GetProperty("price").GetProperty("amount").GetString());
        Assert.Equal("USD", variants[1].GetProperty("price").GetProperty("currency").GetString());
        Assert.Equal(JsonValueKind.Null, variants[1].GetProperty("compareAtPrice").ValueKind);
        Assert.Equal("30.00", variants[2].GetProperty("compareAtPrice").GetProperty("amount").GetString());

        Assert.Equal(
            new[] { "front", "back" },
            root.GetProperty("images").EnumerateArray().Select(i => i.GetProperty("altText").GetString()).ToArray());
    }

    [Theory]
    [InlineData("5", "5.00")]
    [InlineData("24.5", "24.50")]
    [InlineData("12.3456", "12.3456")]
    [InlineData("0", "0.00")]
    [InlineData("1234.5", "1234.50")]
    public async Task Money_amounts_are_strings_formatted_by_the_server(string stored, string wire)
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct("p", "P", variants: [new VariantSpec("P-1", decimal.Parse(stored, System.Globalization.CultureInfo.InvariantCulture))]);
        await env.SaveAsync();

        (HttpStatusCode _, JsonDocument? body) = await env.GetAsync("/api/v1/products/p");

        Assert.Equal(wire, body.RootElement.GetProperty("variants")[0].GetProperty("price").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task Categories_are_listed_with_parent_slug_ordered_by_name()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        env.AddCategory("home", "Home");
        env.AddCategory("clothing", "Clothing");
        env.AddCategory("tops", "Tops", "clothing");
        env.AddCategory("decor", "Decor", "home");
        env.AddCategory("empty-one", "Empty One");
        await env.SaveAsync();

        (HttpStatusCode status, JsonDocument? body) = await env.GetAsync("/api/v1/categories");

        Assert.Equal(HttpStatusCode.OK, status);
        JsonElement[] items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[] { "Clothing", "Decor", "Empty One", "Home", "Tops" }, items.Select(i => i.GetProperty("name").GetString()).ToArray());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("parentSlug").ValueKind);
        Assert.Equal("home", items[1].GetProperty("parentSlug").GetString());
        Assert.Equal("clothing", items[4].GetProperty("parentSlug").GetString());
    }
}
