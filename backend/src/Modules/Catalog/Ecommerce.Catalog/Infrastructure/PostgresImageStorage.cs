using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Catalog.Domain;
using Ecommerce.Persistence;
using Ecommerce.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure;

/// <summary>Image bytes in image_blobs: one INSERT per upload, one primary-key read per view. No external call.</summary>
internal sealed class PostgresImageStorage(EcommerceDbContext db, IClock clock) : IImageStorage
{
    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        using MemoryStream copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        db.Set<ImageBlob>().Add(ImageBlob.Create(key, contentType, copy.ToArray(), clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public Task<StoredImage?> GetAsync(string key, CancellationToken ct) =>
        db.Set<ImageBlob>().AsNoTracking()
            .Where(b => b.StorageKey == key)
            .Select(b => new StoredImage(b.ContentType, b.Content, b.Sha256, b.CreatedAt))
            .FirstOrDefaultAsync(ct);
}
