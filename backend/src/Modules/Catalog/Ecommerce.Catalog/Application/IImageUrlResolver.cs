namespace Ecommerce.Catalog.Application;

internal interface IImageUrlResolver
{
    /// <summary>Returns the public URL for a storage key, or null when no image base URL is configured.</summary>
    string? Resolve(string storageKey);
}
