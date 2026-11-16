using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Orders.Api;

// Query values arrive as string[] so a repeated key reads its first value, and page/pageSize as text so a bad value gets the same errors body as the other 400s (D9).
internal sealed record AdminOrderListRawQuery(
    [FromQuery] string[]? Page,
    [FromQuery] string[]? PageSize,
    [FromQuery] string[]? Status,
    [FromQuery] string[]? From,
    [FromQuery] string[]? To,
    [FromQuery] string[]? Customer,
    [FromQuery] string[]? OrderNumber);
