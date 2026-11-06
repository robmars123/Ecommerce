using Ecommerce.Catalog.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.Api;

[ApiController]
[AllowAnonymous]
[Route("api/v1/categories")]
internal sealed class CategoriesController(CatalogBrowseService catalog) : ControllerBase
{
    [HttpGet("")]
    public async Task<ActionResult<CategoryListResponse>> List(CancellationToken ct) =>
        Ok((await catalog.GetCategoriesAsync(ct)).ToResponse());
}
