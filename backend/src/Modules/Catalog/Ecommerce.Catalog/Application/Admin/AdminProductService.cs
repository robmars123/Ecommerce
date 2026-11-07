using Ecommerce.Catalog.Domain;
using Ecommerce.Inventory.Contracts;
using Ecommerce.SharedKernel.Domain;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Catalog.Application.Admin;

/// <summary>
/// Admin product writes. The server stores exactly the decimals the admin typed and never computes a price.
/// Saves go through <see cref="IUnitOfWork"/>; a stale <c>version</c> becomes a Conflict (optimistic concurrency, D12).
/// </summary>
internal sealed class AdminProductService(
    IProductWriteStore products,
    IAdminCatalogReadStore reads,
    IInventoryReader inventory,
    IImageUrlResolver images,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const string SlugConstraint = "ux_products_slug";
    public const string SkuConstraint = "ux_product_variants_sku";

    private const string StaleMessage = "This product was changed by someone else. Reload and try again.";

    public async Task<AdminResult<AdminProductView>> CreateAsync(CreateProductCommand command, CancellationToken ct)
    {
        Guid? categoryId = await products.FindCategoryIdBySlugAsync(command.CategorySlug.Trim(), ct);
        if (categoryId is null)
        {
            return AdminResult<AdminProductView>.Invalid.For("categorySlug", "That category does not exist.");
        }

        DateTimeOffset now = clock.UtcNow;
        Product product;
        try
        {
            product = Product.Create(
                NewId(now), command.Slug.Trim(), command.Name.Trim(), command.Description.Trim(), categoryId.Value, now);
        }
        catch (DomainException ex)
        {
            return AdminResult<AdminProductView>.Invalid.For("product", ex.Message);
        }

        AdminResult<AdminProductView>? variantError = TryApply("variants", () =>
        {
            foreach (VariantInput v in command.Variants)
            {
                // Created products start with every variant active; the product itself stays unpublished.
                product.AddVariant(
                    NewId(now), v.Sku.Trim(), v.Name.Trim(), Clean(v.Size), Clean(v.Colour), v.Price, v.Currency, v.CompareAtPrice, v.WeightGrams, now);
            }
        });
        if (variantError is not null)
        {
            return variantError;
        }

        AdminResult<AdminProductView>? imageError = TryApply("images", () =>
        {
            for (int i = 0; i < command.Images.Count; i++)
            {
                product.AddImage(NewId(now), command.Images[i].StorageKey.Trim(), command.Images[i].AltText.Trim(), i, now);
            }
        });
        if (imageError is not null)
        {
            return imageError;
        }

        products.Add(product);
        AdminResult<AdminProductView>? failure = await SaveAsync(ct);
        return failure ?? await ViewAsync(product.Id, ct);
    }

    public async Task<AdminResult<AdminProductView>> UpdateAsync(Guid id, UpdateProductCommand command, CancellationToken ct)
    {
        Product? product = await products.GetForUpdateAsync(id, ct);
        if (product is null)
        {
            return new AdminResult<AdminProductView>.NotFound();
        }

        // Set before anything else changes, so the save always carries the version check (even if only children change).
        products.SetExpectedVersion(product, command.Version);

        Guid? categoryId = await products.FindCategoryIdBySlugAsync(command.CategorySlug.Trim(), ct);
        if (categoryId is null)
        {
            return AdminResult<AdminProductView>.Invalid.For("categorySlug", "That category does not exist.");
        }

        string slug = command.Slug.Trim();
        if (product.IsActive && slug != product.Slug)
        {
            return AdminResult<AdminProductView>.Invalid.For("slug", "The slug of a published product cannot change. Unpublish it first.");
        }

        VariantPlan plan = PlanVariants(product, command.Variants);
        if (plan.Error is not null)
        {
            return plan.Error;
        }
        if (product.IsActive && !plan.Updates.Concat(plan.Additions).Any(v => v.IsActive))
        {
            return new AdminResult<AdminProductView>.Conflict(
                "variants", "A published product needs at least one active variant. Unpublish it first.");
        }

        DateTimeOffset now = clock.UtcNow;
        AdminResult<AdminProductView>? applyError = TryApply("product", () =>
        {
            product.UpdateDetails(slug, command.Name.Trim(), command.Description.Trim(), categoryId.Value, now);
        });
        applyError ??= TryApply("variants", () =>
        {
            foreach (VariantInput v in plan.Additions)
            {
                product.AddVariant(
                    NewId(now), v.Sku.Trim(), v.Name.Trim(), Clean(v.Size), Clean(v.Colour), v.Price, v.Currency, v.CompareAtPrice, v.WeightGrams, now);
            }
            // Activations first, then deactivations: swapping the active variant in one request must not trip the
            // "last active variant" rule halfway through.
            foreach (VariantInput? v in plan.Updates.OrderByDescending(u => u.IsActive))
            {
                product.UpdateVariant(
                    v.Id!.Value, v.Name.Trim(), Clean(v.Size), Clean(v.Colour), v.Price, v.CompareAtPrice, v.WeightGrams, v.IsActive, now);
            }
        });
        applyError ??= TryApply("images", () =>
        {
            List<(Guid, string, string)> replacement = command.Images
                .Select(i => (NewId(now), i.StorageKey.Trim(), i.AltText.Trim())).ToList();
            products.RemoveImages(product.ReplaceImages(replacement, now));
        });
        if (applyError is not null)
        {
            return applyError;
        }

        AdminResult<AdminProductView>? failure = await SaveAsync(ct);
        return failure ?? await ViewAsync(product.Id, ct);
    }

    public Task<AdminResult<AdminProductView>> PublishAsync(Guid id, CancellationToken ct) =>
        ChangeVisibilityAsync(id, publish: true, ct);

    public Task<AdminResult<AdminProductView>> UnpublishAsync(Guid id, CancellationToken ct) =>
        ChangeVisibilityAsync(id, publish: false, ct);

    public Task<AdminResult<AdminProductView>> GetAsync(Guid id, CancellationToken ct) => ViewAsync(id, ct);

    public async Task<AdminProductPage> ListAsync(AdminProductListCriteria criteria, CancellationToken ct)
    {
        AdminProductPageRows rows = await reads.ListAsync(criteria, ct);
        int totalPages = (int)Math.Ceiling(rows.TotalCount / (double)criteria.PageSize);
        return new AdminProductPage(rows.Items, criteria.Page, criteria.PageSize, rows.TotalCount, totalPages);
    }

    public async Task<AdminResult<AdminPreview>> PreviewAsync(Guid id, CancellationToken ct)
    {
        AdminPreviewRow? preview = await reads.GetPreviewAsync(id, ct);
        if (preview is null)
        {
            return new AdminResult<AdminPreview>.NotFound();
        }

        ProductDetailRow row = preview.Product;
        IReadOnlyDictionary<Guid, StockStatus> statuses = row.Variants.Count == 0
            ? new Dictionary<Guid, StockStatus>()
            : await inventory.GetStockStatusesAsync(row.Variants.Select(v => v.Id).ToList(), ct);

        ProductDetail detail = new ProductDetail(
            row.Id, row.Slug, row.Name, row.Description, row.Category,
            row.Images.Select(i => new ImageView(images.Resolve(i.StorageKey), i.AltText)).ToList(),
            row.Variants.Select(v => new VariantView(
                v.Id, v.Sku, v.Name, v.Size, v.Colour, v.Price, v.CompareAtPrice, v.Currency,
                statuses.GetValueOrDefault(v.Id, StockStatus.Out))).ToList());
        return new AdminResult<AdminPreview>.Ok(
            new AdminPreview(detail, preview.IsActive, preview.IsActive && row.Variants.Count > 0));
    }

    private async Task<AdminResult<AdminProductView>> ChangeVisibilityAsync(Guid id, bool publish, CancellationToken ct)
    {
        Product? product = await products.GetForUpdateAsync(id, ct);
        if (product is null)
        {
            return new AdminResult<AdminProductView>.NotFound();
        }

        try
        {
            if (publish)
            {
                product.Publish(clock.UtcNow);
            }
            else
            {
                product.Unpublish(clock.UtcNow);
            }
        }
        catch (DomainException ex)
        {
            return new AdminResult<AdminProductView>.Conflict("variants", ex.Message);
        }

        AdminResult<AdminProductView>? failure = await SaveAsync(ct);
        return failure ?? await ViewAsync(id, ct);
    }

    private async Task<AdminResult<AdminProductView>> ViewAsync(Guid id, CancellationToken ct)
    {
        AdminProductRow? row = await reads.GetAsync(id, ct);
        if (row is null)
        {
            return new AdminResult<AdminProductView>.NotFound();
        }
        return new AdminResult<AdminProductView>.Ok(new AdminProductView(
            row.Id, row.Slug, row.Name, row.Description, row.Category, row.IsActive, row.Version, row.CreatedAt, row.UpdatedAt,
            row.Variants,
            row.Images.Select(i => new AdminImageView(i.StorageKey, i.AltText, i.SortOrder, images.Resolve(i.StorageKey))).ToList()));
    }

    /// <summary>Saves, translating the failures an admin can cause. Null means saved.</summary>
    private async Task<AdminResult<AdminProductView>?> SaveAsync(CancellationToken ct)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return null;
        }
        catch (ConcurrencyConflictException)
        {
            return new AdminResult<AdminProductView>.Conflict("version", StaleMessage);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == SlugConstraint)
        {
            return new AdminResult<AdminProductView>.Conflict("slug", "A product with this slug already exists.");
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == SkuConstraint)
        {
            return new AdminResult<AdminProductView>.Conflict("variants", "One of these SKUs is already used by another product.");
        }
    }

    private sealed record VariantPlan(
        IReadOnlyList<VariantInput> Updates,
        IReadOnlyList<VariantInput> Additions,
        AdminResult<AdminProductView>? Error);

    /// <summary>D15: every existing variant must be present by id; SKU and currency of an existing variant are immutable.</summary>
    private static VariantPlan PlanVariants(Product product, IReadOnlyList<VariantInput> inputs)
    {
        static VariantPlan Fail(string message) =>
            new([], [], AdminResult<AdminProductView>.Invalid.For("variants", message));

        Dictionary<Guid, ProductVariant> existing = product.Variants.ToDictionary(v => v.Id);
        List<VariantInput> updates = inputs.Where(v => v.Id is not null).ToList();
        List<VariantInput> additions = inputs.Where(v => v.Id is null).ToList();

        if (updates.Select(v => v.Id).Distinct().Count() != updates.Count)
        {
            return Fail("A variant id appears more than once.");
        }
        foreach (VariantInput? update in updates)
        {
            if (!existing.TryGetValue(update.Id!.Value, out ProductVariant? current))
            {
                return Fail("A variant id does not belong to this product.");
            }
            if (update.Sku.Trim() != current.Sku || update.Currency != current.Currency)
            {
                return Fail("The SKU and currency of an existing variant cannot be changed.");
            }
        }
        if (existing.Keys.Any(id => updates.All(u => u.Id != id)))
        {
            return Fail("Every existing variant must be included. Set isActive to false instead of removing one.");
        }
        return new VariantPlan(updates, additions, null);
    }

    /// <summary>Runs domain calls, turning a rule violation into Invalid on the given field. Null means success.</summary>
    private static AdminResult<AdminProductView>? TryApply(string field, Action apply)
    {
        try
        {
            apply();
            return null;
        }
        catch (DomainException ex)
        {
            return AdminResult<AdminProductView>.Invalid.For(field, ex.Message);
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Time-ordered ids from the injected clock; never the parameterless overload (system clock).
    private static Guid NewId(DateTimeOffset now) => Guid.CreateVersion7(now);
}
