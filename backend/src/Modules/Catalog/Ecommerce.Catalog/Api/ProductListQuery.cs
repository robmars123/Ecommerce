using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.Api;

// Query values arrive as string[] so a repeated key reads its first value, and page/pageSize as text so a bad value gets the same errors body as the other 400s (D9).
internal sealed record ProductListQuery(
    [FromQuery] string[]? Search,
    [FromQuery] string[]? Category,
    [FromQuery] string[]? Sort,
    [FromQuery] string[]? Page,
    [FromQuery] string[]? PageSize);
