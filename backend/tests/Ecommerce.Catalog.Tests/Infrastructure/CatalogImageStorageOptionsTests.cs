using Ecommerce.Catalog.Infrastructure;

namespace Ecommerce.Catalog.Tests.Infrastructure;

public class CatalogImageStorageOptionsTests
{
    private static CatalogImageStorageOptions Full(string url = "https://storage.example.test") => new()
    {
        ServiceUrl = url, Region = "r", Bucket = "b", AccessKeyId = "k", SecretAccessKey = "s",
    };

    [Fact]
    public void All_blank_is_valid_and_not_configured()
    {
        CatalogImageStorageOptions o = new CatalogImageStorageOptions();

        Assert.True(CatalogImageStorageOptions.IsValid(o));
        Assert.False(o.IsConfigured);
    }

    [Fact]
    public void Any_one_value_alone_is_invalid()
    {
        Assert.False(CatalogImageStorageOptions.IsValid(new() { ServiceUrl = "https://storage.example.test" }));
        Assert.False(CatalogImageStorageOptions.IsValid(new() { Bucket = "b" }));
        Assert.False(CatalogImageStorageOptions.IsValid(new() { SecretAccessKey = "s" }));
    }

    [Fact]
    public void All_set_needs_an_https_service_url()
    {
        Assert.False(CatalogImageStorageOptions.IsValid(Full("http://storage.example.test")));
        Assert.False(CatalogImageStorageOptions.IsValid(Full("not a url")));
        Assert.True(CatalogImageStorageOptions.IsValid(Full()));
        Assert.True(Full().IsConfigured);
    }
}
