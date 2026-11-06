using Ecommerce.Catalog.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.Api;

[ApiController]
[AllowAnonymous]
[Route("api/v1/products")]
internal sealed class ProductsController(CatalogBrowseService catalog) : ControllerBase
{
    [HttpGet("")]
    public async Task<ActionResult<ProductListResponse>> List(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        CriteriaResult parsed = ProductSearchCriteria.Create(search, category, sort, page, pageSize);
        if (!parsed.IsValid)
        {
            foreach ((string? field, string[]? messages) in parsed.Errors!)
            {
                foreach (string message in messages)
                {
                    ModelState.AddModelError(field, message);
                }
            }
            return ValidationProblem(ModelState);
        }

        ProductListPage result = await catalog.SearchAsync(parsed.Criteria!, ct);
        return Ok(result.ToResponse());
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ProductDetailResponse>> Get(string slug, CancellationToken ct)
    {
        ProductDetail? product = await catalog.GetBySlugAsync(slug, ct);
        return product is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found")
            : Ok(product.ToResponse());
    }
}
