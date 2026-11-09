namespace Ecommerce.Catalog.Application.Admin;

/// <summary>The only network edge of the image upload: stores one object under a server-made key.</summary>
internal interface IImageStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);
}

/// <summary>Storage settings are blank (open decision, architecture 2); nothing was sent anywhere.</summary>
internal sealed class ImageStorageNotConfiguredException : Exception
{
    public ImageStorageNotConfiguredException() : base("Image storage is not configured.") { }
}
