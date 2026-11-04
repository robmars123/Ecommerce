using Ecommerce.Catalog.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.Api;

[ApiController]
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
        var parsed = ProductSearchCriteria.Create(search, category, sort, page, pageSize);
        if (!parsed.IsValid)
        {
            foreach (var (field, messages) in parsed.Errors!)
            {
                foreach (var message in messages)
                {
                    ModelState.AddModelError(field, message);
                }
            }
            return ValidationProblem(ModelState);
        }

        var result = await catalog.SearchAsync(parsed.Criteria!, ct);
        return Ok(result.ToResponse());
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ProductDetailResponse>> Get(string slug, CancellationToken ct)
    {
        var product = await catalog.GetBySlugAsync(slug, ct);
        return product is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found")
            : Ok(product.ToResponse());
    }
}
