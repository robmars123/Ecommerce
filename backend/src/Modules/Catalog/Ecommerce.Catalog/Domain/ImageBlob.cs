using System.Security.Cryptography;

namespace Ecommerce.Catalog.Domain;

/// <summary>
/// The bytes of one stored product image (table image_blobs), looked up by the same storage key the product keeps. The API serves
/// these bytes itself, so only the three formats we sniff are accepted and the size limit is enforced here too.
/// </summary>
internal sealed class ImageBlob
{
    private ImageBlob() { }

    public string StorageKey { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public int SizeBytes { get; private set; }
    public byte[] Sha256 { get; private set; } = [];
    public byte[] Content { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }

    public static ImageBlob Create(string storageKey, string contentType, byte[] content, DateTimeOffset now)
    {
        if (!Ecommerce.Catalog.Domain.StorageKey.IsValid(storageKey))
        {
            throw new ArgumentException("Invalid storage key.", nameof(storageKey));
        }
        if (contentType != ImageFormat.Jpeg.ContentType && contentType != ImageFormat.Png.ContentType && contentType != ImageFormat.WebP.ContentType)
        {
            throw new ArgumentException("Only JPEG, PNG or WebP can be stored.", nameof(contentType));
        }
        if (content.Length == 0 || content.Length > ImageFormat.MaxBytes)
        {
            throw new ArgumentException("An image must be 1 byte to 5 MB.", nameof(content));
        }
        return new ImageBlob
        {
            StorageKey = storageKey,
            ContentType = contentType,
            SizeBytes = content.Length,
            Sha256 = SHA256.HashData(content),
            Content = content,
            CreatedAt = now,
        };
    }
}
