using Ecommerce.Catalog.Infrastructure;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Tests.Infrastructure;

public class ConfiguredImageUrlResolverTests
{
    private static ConfiguredImageUrlResolver Resolver(string baseUrl) =>
        new(Options.Create(new CatalogImageOptions { BaseUrl = baseUrl }));

    [Fact]
    public void A_blank_base_gives_the_api_image_path_with_each_segment_escaped()
    {
        Assert.Equal("/api/v1/images/products/a%20b/x.png", Resolver("").Resolve("products/a b/x.png"));
    }

    [Fact]
    public void A_configured_base_is_used_as_before()
    {
        Assert.Equal("https://img.example.test/base/products/a%20b/x.png", Resolver("https://img.example.test/base/").Resolve("products/a b/x.png"));
    }
}
