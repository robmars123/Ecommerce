using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Inventory.Api;

// Query values arrive as string[] so a repeated key reads its first value, and page/pageSize as text so a bad value gets the same errors body as the other 400s (D9).
internal sealed record InventoryListQuery(
    [FromQuery] string[]? Page,
    [FromQuery] string[]? PageSize);
