using System.Security.Cryptography;
using Ecommerce.Catalog.Domain;

namespace Ecommerce.Catalog.Tests.Domain;

public class ImageBlobTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Key = "products/a/b.png";

    [Fact]
    public void An_empty_file_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => ImageBlob.Create(Key, ImageFormat.Png.ContentType, [], Now));
    }

    [Fact]
    public void A_file_over_the_limit_is_rejected_and_one_exactly_at_the_limit_is_accepted()
    {
        Assert.Throws<ArgumentException>(() => ImageBlob.Create(Key, ImageFormat.Png.ContentType, new byte[ImageFormat.MaxBytes + 1], Now));

        ImageBlob blob = ImageBlob.Create(Key, ImageFormat.Png.ContentType, new byte[ImageFormat.MaxBytes], Now);

        Assert.Equal(ImageFormat.MaxBytes, blob.SizeBytes);
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("image/svg+xml")]
    [InlineData("")]
    public void A_type_outside_jpeg_png_webp_is_rejected(string contentType)
    {
        Assert.Throws<ArgumentException>(() => ImageBlob.Create(Key, contentType, [1, 2, 3], Now));
    }

    [Fact]
    public void A_valid_blob_keeps_its_fields_and_the_sha256_of_the_bytes()
    {
        byte[] bytes = [1, 2, 3, 4];

        ImageBlob blob = ImageBlob.Create(Key, ImageFormat.WebP.ContentType, bytes, Now);

        Assert.Equal(Key, blob.StorageKey);
        Assert.Equal("image/webp", blob.ContentType);
        Assert.Equal(4, blob.SizeBytes);
        Assert.Equal(bytes, blob.Content);
        Assert.Equal(SHA256.HashData(bytes), blob.Sha256);
        Assert.Equal(Now, blob.CreatedAt);
    }
}
