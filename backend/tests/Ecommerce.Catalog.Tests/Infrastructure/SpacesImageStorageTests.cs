using Amazon.S3;
using Amazon.S3.Model;
using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Catalog.Infrastructure;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Tests.Infrastructure;

public class SpacesImageStorageTests
{
    [Fact]
    public async Task Blank_config_throws_not_configured_before_any_client_is_created()
    {
        SpacesImageStorage storage = new SpacesImageStorage(Options.Create(new CatalogImageStorageOptions()));

        await Assert.ThrowsAsync<ImageStorageNotConfiguredException>(() =>
            storage.PutAsync("products/a/b.png", new MemoryStream([1]), "image/png", TestContext.Current.CancellationToken));
        storage.Dispose();
    }

    [Fact]
    public void The_request_is_public_read_immutable_cached_and_unchunked()
    {
        using MemoryStream body = new MemoryStream([1, 2]);

        PutObjectRequest r = SpacesImageStorage.BuildRequest("bucket-x", "products/a/b.png", body, "image/png");

        Assert.Equal("bucket-x", r.BucketName);
        Assert.Equal("products/a/b.png", r.Key);
        Assert.Equal("image/png", r.ContentType);
        Assert.Same(body, r.InputStream);
        Assert.Equal(S3CannedACL.PublicRead, r.CannedACL);
        Assert.False(r.UseChunkEncoding);
        Assert.Equal("public, max-age=31536000, immutable", r.Headers.CacheControl);
    }
}
