using Ecommerce.Catalog.Application;
using Ecommerce.Catalog.Application.Admin;

namespace Ecommerce.Catalog.Tests.Application;

public class ImageFileServiceTests
{
    private sealed class CountingStorage : IImageStorage
    {
        public int Gets { get; private set; }

        public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct) => throw new NotSupportedException();

        public Task<StoredImage?> GetAsync(string key, CancellationToken ct)
        {
            Gets++;
            return Task.FromResult<StoredImage?>(new StoredImage("image/png", [1], [2], DateTimeOffset.UnixEpoch));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("products/../x.png")]
    [InlineData("products//x.png")]
    [InlineData("/products/x.png")]
    [InlineData("products\\x.png")]
    public async Task An_invalid_key_returns_null_and_storage_is_never_called(string key)
    {
        CountingStorage storage = new CountingStorage();

        StoredImage? image = await new ImageFileService(storage).GetAsync(key, TestContext.Current.CancellationToken);

        Assert.Null(image);
        Assert.Equal(0, storage.Gets);
    }

    [Fact]
    public async Task A_valid_key_is_read_from_storage()
    {
        CountingStorage storage = new CountingStorage();

        StoredImage? image = await new ImageFileService(storage).GetAsync("products/a/b.png", TestContext.Current.CancellationToken);

        Assert.NotNull(image);
        Assert.Equal(1, storage.Gets);
    }
}
