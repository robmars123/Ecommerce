using Ecommerce.Catalog.Application;
using Ecommerce.Catalog.Application.Admin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace Ecommerce.Catalog.Api;

internal static class ImageEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("api/v1/images/{**key}", Get).AllowAnonymous();
    }

    /// <summary>Public, read-only. Keys are server-made and never overwritten, so the bytes are immutable.</summary>
    // key is nullable so an empty tail reaches this method instead of failing binding with a different 400 body.
    private static async Task<Results<FileContentHttpResult, ValidationProblem, ProblemHttpResult>> Get(
        string? key,
        HttpResponse response,
        [FromServices] ImageFileService images,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(key))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["key"] = ["The key field is required."] });
        }

        StoredImage? image = await images.GetAsync(key, ct);
        if (image is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        response.Headers.CacheControl = "public, max-age=31536000, immutable";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        return TypedResults.File(image.Content, image.ContentType, lastModified: image.CreatedAt, entityTag: new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(image.Sha256)}\""));
    }
}
