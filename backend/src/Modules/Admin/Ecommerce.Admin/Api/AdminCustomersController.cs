using Ecommerce.Admin.Application;
using Ecommerce.Identity.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Admin.Api;

internal sealed record AdminCustomerItemResponse(Guid Id, string Email, string DisplayName, DateTimeOffset CreatedAt);

internal sealed record AdminCustomerListResponse(IReadOnlyList<AdminCustomerItemResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);

internal sealed record AdminCustomerOrderResponse(Guid Id, string OrderNumber, DateTimeOffset CreatedAt, string Status, MoneyResponse GrandTotal);

internal sealed record AdminCustomerReturnResponse(
    Guid Id, Guid OrderId, string OrderNumber, string Status, string Reason, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt);

internal sealed record AdminCustomerRefundResponse(
    Guid Id, Guid OrderId, string OrderNumber, MoneyResponse Amount, string Status, bool Automatic, DateTimeOffset CreatedAt);

internal sealed record AdminCustomerDetailResponse(
    Guid Id,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAt,
    int OrderCount,
    MoneyResponse? TotalSpent,
    IReadOnlyList<AdminCustomerOrderResponse> RecentOrders,
    int ReturnCount,
    IReadOnlyList<AdminCustomerReturnResponse> RecentReturns,
    int RefundCount,
    IReadOnlyList<AdminCustomerRefundResponse> RecentRefunds);

/// <summary>Admin and Support, read-only (app-context 12.1). Thin: validate the query, call the service, map. Support never sees the total spent.</summary>
[ApiController]
[Route("api/v1/admin/customers")]
[Authorize(Policy = AuthorizationPolicies.AdminOrSupport)]
internal sealed class AdminCustomersController(CustomerQueryService customers) : ControllerBase
{
    private const int MaxPageSize = 100;
    private const int MaxSearchLength = 100;

    [HttpGet("")]
    public async Task<ActionResult<AdminCustomerListResponse>> List(
        [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (page is < 1)
        {
            ModelState.AddModelError("page", "page must be 1 or greater.");
        }
        if (pageSize is < 1 or > MaxPageSize)
        {
            ModelState.AddModelError("pageSize", $"pageSize must be between 1 and {MaxPageSize}.");
        }
        if (term is { Length: > MaxSearchLength })
        {
            ModelState.AddModelError("search", $"search must be at most {MaxSearchLength} characters.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        int currentPage = page ?? 1;
        int size = pageSize ?? 20;
        CustomerListView view = await customers.ListAsync(term, currentPage, size, ct);
        return Ok(new AdminCustomerListResponse(
            view.Items.Select(c => new AdminCustomerItemResponse(c.Id, c.Email, c.DisplayName, c.CreatedAt)).ToList(),
            currentPage, size, view.TotalCount, (view.TotalCount + size - 1) / size));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminCustomerDetailResponse>> Get(Guid id, CancellationToken ct)
    {
        // Support never sees revenue: the guard is the role, not the client (same rule as the dashboard).
        CustomerDetailView? view = await customers.GetAsync(id, User.IsInRole(IdentityRoles.Admin), ct);
        if (view is null)
        {
            return NotFound();
        }

        string currency = view.Currency;
        return Ok(new AdminCustomerDetailResponse(
            view.Customer.Id, view.Customer.Email, view.Customer.DisplayName, view.Customer.CreatedAt,
            view.OrderCount,
            view.TotalSpent is decimal total ? MoneyResponse.From(total, currency) : null,
            view.RecentOrders.Select(o => new AdminCustomerOrderResponse(o.Id, o.OrderNumber, o.CreatedAt, o.Status, MoneyResponse.From(o.GrandTotal, o.Currency))).ToList(),
            view.ReturnCount,
            view.RecentReturns.Select(r => new AdminCustomerReturnResponse(r.Id, r.OrderId, r.OrderNumber, r.Status, r.Reason, r.CreatedAt, r.DecidedAt)).ToList(),
            view.RefundCount,
            view.RecentRefunds.Select(r => new AdminCustomerRefundResponse(
                r.Refund.RefundId, r.Refund.OrderId, r.OrderNumber, MoneyResponse.From(r.Refund.Amount, r.Refund.Currency), r.Refund.Status, r.Refund.Automatic, r.Refund.CreatedAt)).ToList()));
    }
}
