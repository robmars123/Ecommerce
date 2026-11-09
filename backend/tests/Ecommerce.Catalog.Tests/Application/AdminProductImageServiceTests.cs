using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Catalog.Domain;

namespace Ecommerce.Catalog.Tests.Application;

internal sealed class FakeImageStorage : IImageStorage
{
    public List<(string Key, string ContentType, byte[] Bytes)> Puts { get; } = [];
    public Exception? Throw { get; set; }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        if (Throw is not null)
        {
            throw Throw;
        }
        using MemoryStream copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        Puts.Add((key, contentType, copy.ToArray()));
    }
}

public class AdminProductImageServiceTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5];

    private readonly AdminEnv _env = new();
    private readonly FakeImageStorage _storage = new();

    private AdminProductImageService Service() =>
        new(_env.Reads, _storage, _env.Clock);

    private static UploadImageCommand Command(byte[] bytes, long? length = null) =>
        new(new MemoryStream(bytes), length ?? bytes.Length);

    private static string FileError(AdminResult<UploadedImageView> r) =>
        Assert.Single(Assert.IsType<AdminResult<UploadedImageView>.Invalid>(r).Errors["file"]);

    [Fact]
    public async Task An_unknown_product_is_not_found_and_nothing_is_stored()
    {
        AdminResult<UploadedImageView> r = await Service().UploadAsync(Guid.NewGuid(), Command(Png), TestContext.Current.CancellationToken);

        Assert.IsType<AdminResult<UploadedImageView>.NotFound>(r);
        Assert.Empty(_storage.Puts);
    }

    [Fact]
    public async Task An_empty_file_is_invalid_on_file()
    {
        Guid id = _env.SeedProduct().Id;

        AdminResult<UploadedImageView> r = await Service().UploadAsync(id, Command([]), TestContext.Current.CancellationToken);

        Assert.Equal("Choose an image file.", FileError(r));
        Assert.Empty(_storage.Puts);
    }

    [Fact]
    public async Task A_file_over_the_limit_is_invalid_on_file_and_one_byte_at_the_limit_is_accepted()
    {
        Guid id = _env.SeedProduct().Id;

        AdminResult<UploadedImageView> over = await Service().UploadAsync(id, Command(Png, ImageFormat.MaxBytes + 1), TestContext.Current.CancellationToken);
        AdminResult<UploadedImageView> at = await Service().UploadAsync(id, Command(Png, ImageFormat.MaxBytes), TestContext.Current.CancellationToken);

        Assert.Equal("Images must be 5 MB or smaller.", FileError(over));
        Assert.IsType<AdminResult<UploadedImageView>.Ok>(at);
        Assert.Single(_storage.Puts);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    [InlineData("GIF89a\u0001\u0000\u0001\u0000")]
    public async Task A_non_image_signature_is_invalid_on_file(string text)
    {
        Guid id = _env.SeedProduct().Id;

        AdminResult<UploadedImageView> r = await Service().UploadAsync(id, Command(System.Text.Encoding.Latin1.GetBytes(text)), TestContext.Current.CancellationToken);

        Assert.Equal("Only JPEG, PNG or WebP images are allowed.", FileError(r));
        Assert.Empty(_storage.Puts);
    }

    [Fact]
    public async Task A_valid_image_is_stored_byte_identical_under_a_server_made_v7_key()
    {
        Guid id = _env.SeedProduct().Id;

        AdminResult<UploadedImageView> r = await Service().UploadAsync(id, Command(Png), TestContext.Current.CancellationToken);

        string key = Assert.IsType<AdminResult<UploadedImageView>.Ok>(r).Value.StorageKey;
        (string Key, string ContentType, byte[] Bytes) put = Assert.Single(_storage.Puts);
        Assert.Equal(key, put.Key);
        Assert.Equal("image/png", put.ContentType);
        Assert.Equal(Png, put.Bytes);
        Assert.StartsWith($"products/{id:D}/", key);
        Assert.True(StorageKey.IsValid(key));
        Guid imageId = Guid.Parse(key[(key.LastIndexOf('/') + 1)..^".png".Length]);
        Assert.Equal(7, imageId.Version);
        Assert.Equal(AdminEnv.T0.ToUnixTimeMilliseconds(), ToUnixMs(imageId));
    }

    [Fact]
    public async Task A_storage_that_is_not_configured_is_unavailable_and_other_failures_propagate()
    {
        Guid id = _env.SeedProduct().Id;
        _storage.Throw = new ImageStorageNotConfiguredException();
        AdminResult<UploadedImageView> r = await Service().UploadAsync(id, Command(Png), TestContext.Current.CancellationToken);
        Assert.IsType<AdminResult<UploadedImageView>.Unavailable>(r);

        _storage.Throw = new IOException("down");
        await Assert.ThrowsAsync<IOException>(() => Service().UploadAsync(id, Command(Png), TestContext.Current.CancellationToken));
    }

    /// <summary>The first 48 bits of a UUIDv7 are its Unix milliseconds.</summary>
    private static long ToUnixMs(Guid g)
    {
        Span<byte> b = stackalloc byte[16];
        g.TryWriteBytes(b, bigEndian: true, out _);
        return ((long)b[0] << 40) | ((long)b[1] << 32) | ((long)b[2] << 24) | ((long)b[3] << 16) | ((long)b[4] << 8) | b[5];
    }
}
