using Ecommerce.Catalog.Application;
using Ecommerce.Catalog.Application.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Ecommerce.Catalog.Api;

/// <summary>Public, read-only. Keys are server-made and never overwritten, so the bytes are immutable.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/images")]
internal sealed class ImagesController(ImageFileService images) : ControllerBase
{
    [HttpGet("{**key}")]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        StoredImage? image = await images.GetAsync(key, ct);
        if (image is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(image.Content, image.ContentType, image.CreatedAt, new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(image.Sha256)}\""));
    }
}
