using Ecommerce.Admin.Application;
using Ecommerce.Identity.Contracts;
using Ecommerce.Inventory.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Admin.Api;

internal sealed record SettingsResponse(string Currency, int LowStockThreshold);

/// <summary>Admin only, read-only (P2-tier4 D7): the two values the store runs on. No logic, so no service.</summary>
[ApiController]
[Route("api/v1/admin/settings")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
internal sealed class AdminSettingsController(DashboardSettings dashboard, IInventorySettingsReader inventory) : ControllerBase
{
    [HttpGet("")]
    public ActionResult<SettingsResponse> Get() => Ok(new SettingsResponse(dashboard.Currency, inventory.LowStockThreshold));
}
