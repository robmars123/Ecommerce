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

    private static async Task<Results<Ok<ProductListResponse>, ValidationProblem>> List(
        [AsParameters] ProductListQuery query,
        [FromServices] CatalogBrowseService catalog,
        CancellationToken ct)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        int? pageNumber = QueryValues.ReadInt(query.Page, "page", errors);
        int? pageSizeNumber = QueryValues.ReadInt(query.PageSize, "pageSize", errors);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        CriteriaResult parsed = ProductSearchCriteria.Create(query.Search?.FirstOrDefault(), query.Category?.FirstOrDefault(), query.Sort?.FirstOrDefault(), pageNumber, pageSizeNumber);
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
}
