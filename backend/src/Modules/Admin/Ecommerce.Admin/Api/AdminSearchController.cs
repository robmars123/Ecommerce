using Ecommerce.Admin.Application;
using Ecommerce.Identity.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Admin.Api;

internal sealed record AdminSearchOrderResponse(Guid Id, string OrderNumber, string Status);

internal sealed record AdminSearchProductResponse(Guid Id, string Name);

internal sealed record AdminSearchSkuResponse(string Sku, Guid ProductId, string ProductName);

internal sealed record AdminSearchResponse(
    IReadOnlyList<AdminSearchOrderResponse> Orders,
    IReadOnlyList<AdminSearchProductResponse> Products,
    IReadOnlyList<AdminSearchSkuResponse> Skus);

/// <summary>Admin and Support, read-only. Thin: validate q, call the service, map. Support gets no products or SKUs.</summary>
[ApiController]
[Route("api/v1/admin/search")]
[Authorize(Policy = AuthorizationPolicies.AdminOrSupport)]
internal sealed class AdminSearchController(AdminSearchService search) : ControllerBase
{
    [HttpGet("")]
    public async Task<ActionResult<AdminSearchResponse>> Get([FromQuery] string? q, CancellationToken ct)
    {
        if (!AdminSearchTerm.TryParse(q, out string term, out string? error))
        {
            ModelState.AddModelError("q", error!);
            return ValidationProblem(ModelState);
        }

        AdminSearchView view = await search.SearchAsync(term, User.IsInRole(IdentityRoles.Admin), ct);
        return Ok(new AdminSearchResponse(
            view.Orders.Select(o => new AdminSearchOrderResponse(o.Id, o.OrderNumber, o.Status)).ToList(),
            view.Products.Select(p => new AdminSearchProductResponse(p.Id, p.Name)).ToList(),
            view.Skus.Select(s => new AdminSearchSkuResponse(s.Sku, s.ProductId, s.ProductName)).ToList()));
    }
}
