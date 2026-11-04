using Ecommerce.SharedKernel.Time;
using Ecommerce.Catalog.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.DevSeed;

/// <summary>
/// Idempotent development data. Inserts a category, product or variant only when its slug or SKU is missing;
/// never updates or deletes. Writes through domain factories and EF in one SaveChanges (one transaction).
/// </summary>
internal sealed class CatalogDevSeeder
{
    public async Task SeedAsync(EcommerceDbContext db, IClock clock, CancellationToken ct)
    {
        var now = clock.UtcNow;

        var categoriesBySlug = await db.Set<Category>().ToDictionaryAsync(c => c.Slug, ct);
        foreach (var seed in CatalogDevSeedData.Categories)
        {
            if (categoriesBySlug.ContainsKey(seed.Slug))
            {
                continue;
            }

            var parentId = seed.ParentSlug is null ? (Guid?)null : categoriesBySlug[seed.ParentSlug].Id;
            var category = Category.Create(NewId(clock), seed.Slug, seed.Name, parentId);
            db.Add(category);
            categoriesBySlug[seed.Slug] = category;
        }

        var productsBySlug = await db.Set<Product>().Include(p => p.Variants).ToDictionaryAsync(p => p.Slug, ct);
        var existingSkus = (await db.Set<ProductVariant>().Select(v => v.Sku).ToListAsync(ct)).ToHashSet();

        var offset = 0;
        foreach (var seed in CatalogDevSeedData.Products)
        {
            // Spread created_at so "newest" ordering is meaningful.
            var createdAt = now.AddMinutes(-offset++);

            var isNew = !productsBySlug.TryGetValue(seed.Slug, out var product);
            if (isNew)
            {
                product = Product.Create(
                    NewId(clock), seed.Slug, seed.Name, seed.Description, categoriesBySlug[seed.CategorySlug].Id, createdAt);
                db.Add(product);
                productsBySlug[seed.Slug] = product;

                for (var n = 1; n <= seed.ImageCount; n++)
                {
                    product.AddImage(NewId(clock), $"dev-seed/{seed.Slug}/{n}.jpg", $"{seed.Name}, view {n}", n, createdAt);
                }
            }

            var added = new List<(ProductVariant Variant, bool Active)>();
            foreach (var v in seed.Variants.Where(v => !existingSkus.Contains(v.Sku)))
            {
                var variant = product.AddVariant(
                    NewId(clock), v.Sku, v.Name, v.Size, null, v.Price, CatalogDevSeedData.Currency, v.CompareAt, 250, createdAt);
                existingSkus.Add(v.Sku);
                added.Add((variant, v.Active));
            }

            if (isNew && seed.Publish)
            {
                product.Publish(createdAt);
            }

            // Deactivate after publishing: Publish requires an active variant.
            foreach (var (variant, active) in added.Where(a => !a.Active))
            {
                variant.Deactivate();
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // Time-ordered ids from the injected clock; never the parameterless overload (system clock).
    private static Guid NewId(IClock clock) => Guid.CreateVersion7(clock.UtcNow);
}
