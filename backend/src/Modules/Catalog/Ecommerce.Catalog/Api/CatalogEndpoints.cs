using Ecommerce.Catalog.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Catalog.Api;

internal static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder categories = app.MapGroup("api/v1").AllowAnonymous();
        categories.MapGet("categories", ListCategories).WithName("ListCategories");
        return app;
    }

    private static async Task<Ok<CategoryListResponse>> ListCategories([FromServices] CatalogBrowseService catalog, CancellationToken ct) =>
        TypedResults.Ok((await catalog.GetCategoriesAsync(ct)).ToResponse());
}
