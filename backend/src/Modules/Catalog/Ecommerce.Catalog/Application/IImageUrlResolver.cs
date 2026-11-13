namespace Ecommerce.Catalog.Application;

internal interface IImageUrlResolver
{
    /// <summary>The public URL for a storage key: under the configured base URL, or the API's own /api/v1/images route when blank.</summary>
    string Resolve(string storageKey);
}
