using Ecommerce.Admin.Application;
using Ecommerce.Identity.Contracts;
using Ecommerce.Inventory.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Admin.Api;

internal sealed record SettingsResponse(string Currency, int LowStockThreshold);

/// <summary>Admin only, read-only (P2-tier4 D7): the two values the store runs on. No logic, so no service.</summary>
internal static class AdminSettingsEndpoints
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("api/v1/admin/settings", Get).RequireAuthorization(AuthorizationPolicies.Admin);

    private static Ok<SettingsResponse> Get([FromServices] DashboardSettings dashboard, [FromServices] IInventorySettingsReader inventory) =>
        TypedResults.Ok(new SettingsResponse(dashboard.Currency, inventory.LowStockThreshold));
}
