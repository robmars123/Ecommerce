using System.Text.RegularExpressions;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Catalog.Domain;

internal sealed partial class Product
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 4000;
    public const int MaxSkuLength = 64;

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
    public IReadOnlyList<ProductVariant> Variants => _variants;
    public IReadOnlyList<ProductImage> Images => _images;

    public static Product Create(Guid id, string slug, string name, string description, Guid categoryId, DateTimeOffset now)
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
        return new Product
        {
            Id = id, Slug = slug, Name = name, Description = description ?? "", CategoryId = categoryId,
            IsActive = false, CreatedAt = now, UpdatedAt = now,
        };
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
        if (price < 0)
        {
            throw new DomainException("Price cannot be negative.");
        }
        if (compareAtPrice is not null && compareAtPrice <= price)
        {
            throw new DomainException("Compare-at price must be greater than the price.");
        }
        if (string.IsNullOrEmpty(currency) || !CurrencyPattern().IsMatch(currency))
        {
            throw new DomainException("Currency must be a 3-letter upper-case code.");
        }
        if (_variants.Count > 0 && _variants[0].Currency != currency)
        {
            throw new DomainException("All variants of a product must share one currency.");
        }
        if (weightGrams < 0)
        {
            throw new DomainException("Weight cannot be negative.");
        }
        var variant = ProductVariant.Create(id, Id, sku, name, size, colour, price, currency, compareAtPrice, weightGrams);
        _variants.Add(variant);
        UpdatedAt = now;
        return variant;
    }

    public ProductImage AddImage(Guid id, string storageKey, string altText, int sortOrder, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(storageKey) || storageKey.Length > 512)
        {
            throw new DomainException("Image storage key must be 1 to 512 characters.");
        }
        if ((altText ?? "").Length > 300)
        {
            throw new DomainException("Image alt text must be at most 300 characters.");
        }
        var image = ProductImage.Create(id, Id, storageKey, altText ?? "", sortOrder);
        _images.Add(image);
        UpdatedAt = now;
        return image;
    }

    public void Publish(DateTimeOffset now)
    {
        if (!_variants.Any(v => v.IsActive))
        {
            throw new DomainException("A product needs at least one active variant to be published.");
        }
        IsActive = true;
        UpdatedAt = now;
    }

    public void Unpublish(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    [GeneratedRegex(@"^[A-Z0-9-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex SkuPattern();

    [GeneratedRegex(@"^[A-Z]{3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyPattern();
}
