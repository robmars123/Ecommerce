using Ecommerce.Catalog.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Catalog.Api;

internal static class CategoryEndpoints
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("api/v1/categories", List).AllowAnonymous();

    private static async Task<Ok<CategoryListResponse>> List([FromServices] CatalogBrowseService catalog, CancellationToken ct) =>
        TypedResults.Ok((await catalog.GetCategoriesAsync(ct)).ToResponse());
}
