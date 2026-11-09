namespace Ecommerce.Catalog.Domain;

/// <summary>
/// The image formats we publish, decided only by the file signature (never the client's type or name).
/// SVG, GIF, HEIC and anything else are refused: the object is served publicly from the CDN.
/// </summary>
internal sealed record ImageFormat(string ContentType, string Extension)
{
    public const int HeaderLength = 12;
    public const long MaxBytes = 5 * 1024 * 1024;

    public static readonly ImageFormat Jpeg = new("image/jpeg", "jpg");
    public static readonly ImageFormat Png = new("image/png", "png");
    public static readonly ImageFormat WebP = new("image/webp", "webp");

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static ImageFormat? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return Jpeg;
        }
        if (header.StartsWith(PngSignature))
        {
            return Png;
        }
        if (header.Length >= HeaderLength && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return WebP;
        }
        return null;
    }
}
