namespace Ecommerce.Catalog.Application.Admin;

/// <summary>One stored image as the public route serves it.</summary>
internal sealed record StoredImage(string ContentType, byte[] Content, byte[] Sha256, DateTimeOffset CreatedAt);

/// <summary>Stores and reads image bytes under a server-made key (Postgres image_blobs, P2.56).</summary>
internal interface IImageStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);

    Task<StoredImage?> GetAsync(string key, CancellationToken ct);
}
