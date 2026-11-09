using Ecommerce.Catalog.Domain;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Catalog.Application.Admin;

/// <summary>
/// Stores one uploaded image and returns its key. Attaches nothing to the product: the admin sends the key in the next
/// version-checked product update (D2). Nothing is written to Postgres, so no transaction is open during the upload.
/// </summary>
internal sealed class AdminProductImageService(
    IAdminCatalogReadStore reads,
    IImageStorage storage,
    IClock clock)
{
    public const string FileField = "file";

    public async Task<AdminResult<UploadedImageView>> UploadAsync(Guid productId, UploadImageCommand command, CancellationToken ct)
    {
        if (!await reads.ProductExistsAsync(productId, ct))
        {
            return new AdminResult<UploadedImageView>.NotFound();
        }
        if (command.Length == 0)
        {
            return AdminResult<UploadedImageView>.Invalid.For(FileField, "Choose an image file.");
        }
        if (command.Length > ImageFormat.MaxBytes)
        {
            return AdminResult<UploadedImageView>.Invalid.For(FileField, "Images must be 5 MB or smaller.");
        }

        byte[] header = new byte[ImageFormat.HeaderLength];
        int read = await command.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        ImageFormat? format = ImageFormat.Detect(header.AsSpan(0, read));
        if (format is null)
        {
            return AdminResult<UploadedImageView>.Invalid.For(FileField, "Only JPEG, PNG or WebP images are allowed.");
        }
        if (!command.Content.CanSeek)
        {
            throw new InvalidOperationException("The upload stream must be seekable.");
        }
        command.Content.Position = 0;

        string key = StorageKey.ForProductImage(productId, Guid.CreateVersion7(clock.UtcNow), format.Extension);
        try
        {
            await storage.PutAsync(key, command.Content, format.ContentType, ct);
        }
        catch (ImageStorageNotConfiguredException e)
        {
            return new AdminResult<UploadedImageView>.Unavailable(e.Message);
        }

        return new AdminResult<UploadedImageView>.Ok(new UploadedImageView(key));
    }
}
