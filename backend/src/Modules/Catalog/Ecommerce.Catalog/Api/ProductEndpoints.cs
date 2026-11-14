using System.Globalization;
using Ecommerce.Catalog.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Catalog.Api;

internal static class ProductEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/v1").AllowAnonymous();
        group.MapGet("products", List);
        group.MapGet("products/{slug}", Get);
    }

    // page and pageSize arrive as text so a bad value gets the same errors body as the other 400s.
    private static async Task<Results<Ok<ProductListResponse>, ValidationProblem>> List(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? sort,
        [FromQuery] string? page,
        [FromQuery] string? pageSize,
        [FromServices] CatalogBrowseService catalog,
        CancellationToken ct)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        int? pageNumber = ReadInt(page, "page", errors);
        int? pageSizeNumber = ReadInt(pageSize, "pageSize", errors);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        CriteriaResult parsed = ProductSearchCriteria.Create(search, category, sort, pageNumber, pageSizeNumber);
        if (!parsed.IsValid)
        {
            return TypedResults.ValidationProblem(parsed.Errors!);
        }

        ProductListPage result = await catalog.SearchAsync(parsed.Criteria!, ct);
        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<Results<Ok<ProductDetailResponse>, ProblemHttpResult>> Get(
        string slug,
        [FromServices] CatalogBrowseService catalog,
        CancellationToken ct)
    {
        ProductDetail? product = await catalog.GetBySlugAsync(slug, ct);
        return product is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found")
            : TypedResults.Ok(product.ToResponse());
    }

    private static int? ReadInt(string? raw, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return value;
        }

        errors[field] = [$"The value '{raw}' is not valid."];
        return null;
    }
}
