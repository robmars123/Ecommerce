using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Catalog.Domain;

namespace Ecommerce.Catalog.Application;

/// <summary>Reads one stored image for the public route. An invalid key is answered without touching storage.</summary>
internal sealed class ImageFileService(IImageStorage storage)
{
    public Task<StoredImage?> GetAsync(string key, CancellationToken ct) =>
        StorageKey.IsValid(key) ? storage.GetAsync(key, ct) : Task.FromResult<StoredImage?>(null);
}
