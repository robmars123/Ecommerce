namespace Ecommerce.Catalog.Infrastructure.DevSeed;

/// <summary>Fixed in-code dataset for local development. Never used by tests for assertions.</summary>
internal static class CatalogDevSeedData
{
    public sealed record SeedCategory(string Slug, string Name, string? ParentSlug);

    public sealed record SeedVariant(string Sku, string Name, string? Size, decimal Price, decimal? CompareAt, bool Active);

    public sealed record SeedProduct(
        string Slug, string Name, string Description, string CategorySlug,
        bool Publish, IReadOnlyList<SeedVariant> Variants, int ImageCount);

    public const string Currency = "USD";

    public static readonly IReadOnlyList<SeedCategory> Categories =
    [
        new("clothing", "Clothing", null),
        new("tops", "Tops", "clothing"),
        new("bottoms", "Bottoms", "clothing"),
        new("home", "Home", null),
        new("kitchen", "Kitchen", "home"),
        new("decor", "Decor", "home"),
    ];

    private static readonly (string CategorySlug, string[] Names)[] ActiveNames =
    [
        ("tops", ["Blue Shirt", "Red Shirt", "Striped Tee", "Linen Shirt", "Hooded Sweatshirt", "Wool Sweater"]),
        ("bottoms", ["Slim Jeans", "Chino Trousers", "Cargo Shorts", "Pleated Skirt", "Jogger Pants", "Denim Shorts"]),
        ("kitchen", ["Ceramic Mug", "Chef Knife", "Cast Iron Skillet", "Wooden Spoon Set", "Glass Teapot", "Cutting Board"]),
        ("decor", ["Linen Cushion", "Scented Candle", "Wall Clock", "Table Lamp", "Woven Basket", "Picture Frame"]),
    ];

    // Declared before Products: static initialisers run in textual order and Build() reads them.
    private static readonly string[] VariantCodes = ["S", "M", "L"];
    private static readonly string[] VariantNames = ["Small", "Medium", "Large"];

    public static readonly IReadOnlyList<SeedProduct> Products = Build();

    private static List<SeedProduct> Build()
    {
        var products = new List<SeedProduct>();
        var index = 0;
        foreach (var (categorySlug, names) in ActiveNames)
        {
            foreach (var name in names)
            {
                var slug = Slugify(name);
                var variantCount = index % 3 + 1;
                var basePrice = 9.99m + index * 3.5m;
                var variants = Enumerable.Range(0, variantCount)
                    .Select(v => new SeedVariant(
                        Sku: $"{slug.ToUpperInvariant()}-{VariantCodes[v]}",
                        Name: VariantNames[v],
                        Size: VariantCodes[v],
                        Price: basePrice + v * 2m,
                        CompareAt: index % 4 == 0 ? basePrice + v * 2m + 5m : null,
                        Active: true))
                    .ToList();
                products.Add(new SeedProduct(
                    slug, name, $"{name}. A dev-seed product used to exercise browse, search and pagination.",
                    categorySlug, Publish: true, variants, ImageCount: index % 2 + 1));
                index++;
            }
        }

        // Hidden by design: inactive products, and an active product whose only variant is inactive.
        products.Add(Hidden("retired-scarf", "Retired Scarf", "tops"));
        products.Add(Hidden("draft-vase", "Draft Vase", "decor"));
        products.Add(Hidden("hidden-apron", "Hidden Apron", "kitchen"));
        products.Add(new SeedProduct(
            "ghost-hat", "Ghost Hat", "Active product with only an inactive variant; must not be visible.", "tops",
            Publish: true, [new SeedVariant("GHOST-HAT-S", "Small", "S", 14.99m, null, Active: false)], ImageCount: 1));
        return products;
    }

    private static SeedProduct Hidden(string slug, string name, string categorySlug) =>
        new(slug, name, "Inactive dev-seed product; must not be visible.", categorySlug,
            Publish: false, [new SeedVariant($"{slug.ToUpperInvariant()}-S", "Small", "S", 12.99m, null, Active: true)], ImageCount: 1);

    private static string Slugify(string name) => name.ToLowerInvariant().Replace(' ', '-');
}
