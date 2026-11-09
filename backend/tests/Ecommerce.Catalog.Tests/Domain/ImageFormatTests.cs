using System.Text;
using Ecommerce.Catalog.Domain;

namespace Ecommerce.Catalog.Tests.Domain;

public class ImageFormatTests
{
    [Fact]
    public void Each_signature_is_detected_with_its_content_type_and_extension()
    {
        Assert.Equal(ImageFormat.Jpeg, ImageFormat.Detect([0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0]));
        Assert.Equal(ImageFormat.Png, ImageFormat.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]));
        Assert.Equal(ImageFormat.WebP, ImageFormat.Detect([.. "RIFF"u8, 1, 0, 0, 0, .. "WEBP"u8]));
        Assert.Equal(("image/jpeg", "jpg"), (ImageFormat.Jpeg.ContentType, ImageFormat.Jpeg.Extension));
        Assert.Equal(("image/png", "png"), (ImageFormat.Png.ContentType, ImageFormat.Png.Extension));
        Assert.Equal(("image/webp", "webp"), (ImageFormat.WebP.ContentType, ImageFormat.WebP.Extension));
    }

    [Theory]
    [InlineData("")]
    [InlineData("hi")]
    [InlineData("<svg xmlns=\"x\"/>")]
    [InlineData("<?xml version=\"1.0\"?>")]
    [InlineData("%PDF-1.7 abcdef")]
    [InlineData("GIF89a123456")]
    public void Anything_else_is_not_an_image(string text)
    {
        Assert.Null(ImageFormat.Detect(Encoding.ASCII.GetBytes(text)));
    }

    [Fact]
    public void Riff_that_is_not_webp_and_a_damaged_png_signature_are_rejected()
    {
        Assert.Null(ImageFormat.Detect([.. "RIFF"u8, 1, 0, 0, 0, .. "WAVE"u8]));
        Assert.Null(ImageFormat.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0B, 0, 0, 0, 0]));
    }
}
