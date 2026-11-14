using System.Security.Claims;
using Ecommerce.Admin.Application;
using Ecommerce.Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Admin.Api;

internal sealed record AdminSearchOrderResponse(Guid Id, string OrderNumber, string Status);

internal sealed record AdminSearchProductResponse(Guid Id, string Name);

internal sealed record AdminSearchSkuResponse(string Sku, Guid ProductId, string ProductName);

internal sealed record AdminSearchResponse(
    IReadOnlyList<AdminSearchOrderResponse> Orders,
    IReadOnlyList<AdminSearchProductResponse> Products,
    IReadOnlyList<AdminSearchSkuResponse> Skus);

/// <summary>Admin and Support, read-only. Thin: validate q, call the service, map. Support gets no products or SKUs.</summary>
internal static class AdminSearchEndpoints
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("api/v1/admin/search", Get).RequireAuthorization(AuthorizationPolicies.AdminOrSupport);

    // q is a string[] so a repeated ?q=a&q=bb uses the first value, as MVC did (a plain string would join them as "a,bb").
    private static async Task<Results<Ok<AdminSearchResponse>, ValidationProblem>> Get(
        [FromQuery] string[]? q, ClaimsPrincipal user, [FromServices] AdminSearchService search, CancellationToken ct)
    {
        if (!AdminSearchTerm.TryParse(q?.FirstOrDefault(), out string term, out string? error))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["q"] = [error!] });
        }

        AdminSearchView view = await search.SearchAsync(term, user.IsInRole(IdentityRoles.Admin), ct);
        return TypedResults.Ok(new AdminSearchResponse(
            view.Orders.Select(o => new AdminSearchOrderResponse(o.Id, o.OrderNumber, o.Status)).ToList(),
            view.Products.Select(p => new AdminSearchProductResponse(p.Id, p.Name)).ToList(),
            view.Skus.Select(s => new AdminSearchSkuResponse(s.Sku, s.ProductId, s.ProductName)).ToList()));
    }
}
