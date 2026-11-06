using System.Text.RegularExpressions;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Catalog.Domain;

internal sealed partial class Product
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 4000;
    public const int MaxSkuLength = 64;
    public const int MaxVariants = 50;
    public const int MaxImages = 20;
    public const int MaxVariantNameLength = 200;
    public const int MaxVariantOptionLength = 50;
    public const int MaxImageAltTextLength = 300;

    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductImage> _images = [];

    private Product() { }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public Guid CategoryId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token. A primitive here; Infrastructure maps it to the Postgres xmin system column,
    /// so it changes on every committed write to the row and needs no DDL.
    /// </summary>
    public uint Version { get; private set; }

    public IReadOnlyList<ProductVariant> Variants => _variants;
    public IReadOnlyList<ProductImage> Images => _images;

    public static Product Create(Guid id, string slug, string name, string description, Guid categoryId, DateTimeOffset now)
    {
        ValidateDetails(slug, name, description);
        return new Product
        {
            Id = id, Slug = slug, Name = name, Description = description ?? "", CategoryId = categoryId,
            IsActive = false, CreatedAt = now, UpdatedAt = now,
        };
    }

    /// <summary>Same validation as <see cref="Create"/>. A published slug is a public URL, so it cannot change while active.</summary>
    public void UpdateDetails(string slug, string name, string description, Guid categoryId, DateTimeOffset now)
    {
        ValidateDetails(slug, name, description);
        if (IsActive && slug != Slug)
        {
            throw new DomainException("The slug of a published product cannot change. Unpublish it first.");
        }
        Slug = slug;
        Name = name;
        Description = description ?? "";
        CategoryId = categoryId;
        UpdatedAt = now;
    }

    public ProductVariant AddVariant(
        Guid id, string sku, string name, string? size, string? colour,
        decimal price, string currency, decimal? compareAtPrice, int weightGrams, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(sku) || sku.Length > MaxSkuLength || !SkuPattern().IsMatch(sku))
        {
            throw new DomainException("SKU must be 1 to 64 characters of A-Z, 0-9 and hyphen.");
        }
        if (_variants.Any(v => v.Sku == sku))
        {
            throw new DomainException("SKU must be unique within the product.");
        }
        if (_variants.Count >= MaxVariants)
        {
            throw new DomainException($"A product can have at most {MaxVariants} variants.");
        }
        ValidateVariantFields(name, size, colour, price, compareAtPrice, weightGrams);
        if (string.IsNullOrEmpty(currency) || !CurrencyPattern().IsMatch(currency))
        {
            throw new DomainException("Currency must be a 3-letter upper-case code.");
        }
        if (_variants.Count > 0 && _variants[0].Currency != currency)
        {
            throw new DomainException("All variants of a product must share one currency.");
        }
        ProductVariant variant = ProductVariant.Create(id, Id, sku, name, size, colour, price, currency, compareAtPrice, weightGrams);
        _variants.Add(variant);
        UpdatedAt = now;
        return variant;
    }

    /// <summary>
    /// Edits a variant. SKU and currency are immutable (stock and order snapshots key on them). Deactivating the last
    /// active variant of a published product is refused: unpublish first.
    /// </summary>
    public void UpdateVariant(
        Guid variantId, string name, string? size, string? colour, decimal price, decimal? compareAtPrice,
        int weightGrams, bool isActive, DateTimeOffset now)
    {
        ProductVariant variant = _variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new DomainException("That variant does not belong to this product.");
        ValidateVariantFields(name, size, colour, price, compareAtPrice, weightGrams);
        if (!isActive && variant.IsActive && IsActive && _variants.Count(v => v.IsActive) == 1)
        {
            throw new DomainException("A published product needs an active variant. Unpublish it first.");
        }
        variant.Update(name, size, colour, price, compareAtPrice, weightGrams);
        variant.SetActive(isActive);
        UpdatedAt = now;
    }

    public ProductImage AddImage(Guid id, string storageKey, string altText, int sortOrder, DateTimeOffset now)
    {
        ValidateImage(storageKey, altText);
        ProductImage image = ProductImage.Create(id, Id, storageKey, altText ?? "", sortOrder);
        _images.Add(image);
        UpdatedAt = now;
        return image;
    }

    /// <summary>
    /// Replaces the whole image list; the array order becomes sort_order. Validates everything first, so a bad
    /// entry changes nothing. Returns the removed images so the persistence layer can delete them.
    /// </summary>
    public IReadOnlyList<ProductImage> ReplaceImages(IReadOnlyList<(Guid Id, string StorageKey, string AltText)> images, DateTimeOffset now)
    {
        if (images.Count > MaxImages)
        {
            throw new DomainException($"A product can have at most {MaxImages} images.");
        }
        foreach ((Guid Id, string StorageKey, string AltText) image in images)
        {
            ValidateImage(image.StorageKey, image.AltText);
        }

        List<ProductImage> removed = _images.ToList();
        _images.Clear();
        for (int i = 0; i < images.Count; i++)
        {
            _images.Add(ProductImage.Create(images[i].Id, Id, images[i].StorageKey, images[i].AltText ?? "", i));
        }
        UpdatedAt = now;
        return removed;
    }

    /// <summary>Idempotent: publishing a published product changes nothing (not even UpdatedAt).</summary>
    public void Publish(DateTimeOffset now)
    {
        if (IsActive)
        {
            return;
        }
        if (!_variants.Any(v => v.IsActive))
        {
            throw new DomainException("A product needs at least one active variant to be published.");
        }
        IsActive = true;
        UpdatedAt = now;
    }

    /// <summary>Idempotent: unpublishing an unpublished product changes nothing.</summary>
    public void Unpublish(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }
        IsActive = false;
        UpdatedAt = now;
    }

    private static void ValidateDetails(string slug, string name, string description)
    {
        if (!Domain.Slug.IsValid(slug))
        {
            throw new DomainException("Product slug is invalid.");
        }
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            throw new DomainException($"Product name must be 1 to {MaxNameLength} characters.");
        }
        if ((description ?? "").Length > MaxDescriptionLength)
        {
            throw new DomainException($"Product description must be at most {MaxDescriptionLength} characters.");
        }
    }

    private static void ValidateVariantFields(
        string name, string? size, string? colour, decimal price, decimal? compareAtPrice, int weightGrams)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxVariantNameLength)
        {
            throw new DomainException($"Variant name must be 1 to {MaxVariantNameLength} characters.");
        }
        if (size is not null && (size.Length == 0 || size.Length > MaxVariantOptionLength))
        {
            throw new DomainException($"Variant size must be 1 to {MaxVariantOptionLength} characters, or empty.");
        }
        if (colour is not null && (colour.Length == 0 || colour.Length > MaxVariantOptionLength))
        {
            throw new DomainException($"Variant colour must be 1 to {MaxVariantOptionLength} characters, or empty.");
        }
        if (price < 0)
        {
            throw new DomainException("Price cannot be negative.");
        }
        if (compareAtPrice is not null && compareAtPrice <= price)
        {
            throw new DomainException("Compare-at price must be greater than the price.");
        }
        if (weightGrams < 0)
        {
            throw new DomainException("Weight cannot be negative.");
        }
    }

    private static void ValidateImage(string storageKey, string altText)
    {
        if (!StorageKey.IsValid(storageKey))
        {
            throw new DomainException("Image storage key must be a relative key of 1 to 512 characters, without '..', '\\', ':' or control characters.");
        }
        if ((altText ?? "").Length > MaxImageAltTextLength)
        {
            throw new DomainException($"Image alt text must be at most {MaxImageAltTextLength} characters.");
        }
    }

    [GeneratedRegex(@"^[A-Z0-9-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex SkuPattern();

    [GeneratedRegex(@"^[A-Z]{3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyPattern();
}
