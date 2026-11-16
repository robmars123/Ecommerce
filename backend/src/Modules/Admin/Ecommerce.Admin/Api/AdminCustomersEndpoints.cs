using System.Security.Claims;
using Ecommerce.Admin.Application;
using Ecommerce.Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

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
internal static class AdminCustomersEndpoints
{
    private const int MaxPageSize = 100;
    private const int MaxSearchLength = 100;

    public static void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.AdminOrSupport);
        group.MapGet("customers", List);
        group.MapGet("customers/{id:guid}", Get);
    }

    private static async Task<Results<Ok<AdminCustomerListResponse>, ValidationProblem>> List(
        [AsParameters] AdminCustomerListQuery query,
        [FromServices] CustomerQueryService customers,
        CancellationToken ct)
    {
        // Bad numbers are reported alone, before the range checks, as MVC model binding did.
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        int? pageNumber = QueryValues.ReadInt(query.Page, "page", errors);
        int? pageSizeNumber = QueryValues.ReadInt(query.PageSize, "pageSize", errors);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        string? rawSearch = query.Search?.FirstOrDefault();
        string? term = string.IsNullOrWhiteSpace(rawSearch) ? null : rawSearch.Trim();
        if (pageNumber is < 1)
        {
            errors["page"] = ["page must be 1 or greater."];
        }
        if (pageSizeNumber is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"pageSize must be between 1 and {MaxPageSize}."];
        }
        if (term is { Length: > MaxSearchLength })
        {
            errors["search"] = [$"search must be at most {MaxSearchLength} characters."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        int currentPage = pageNumber ?? 1;
        int size = pageSizeNumber ?? 20;
        CustomerListView view = await customers.ListAsync(term, currentPage, size, ct);
        return TypedResults.Ok(new AdminCustomerListResponse(
            view.Items.Select(c => new AdminCustomerItemResponse(c.Id, c.Email, c.DisplayName, c.CreatedAt)).ToList(),
            currentPage, size, view.TotalCount, (view.TotalCount + size - 1) / size));
    }

    private static async Task<Results<Ok<AdminCustomerDetailResponse>, ProblemHttpResult>> Get(
        Guid id, ClaimsPrincipal user, [FromServices] CustomerQueryService customers, CancellationToken ct)
    {
        // Support never sees revenue: the guard is the role, not the client (same rule as the dashboard).
        CustomerDetailView? view = await customers.GetAsync(id, user.IsInRole(IdentityRoles.Admin), ct);
        if (view is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        string currency = view.Currency;
        return TypedResults.Ok(new AdminCustomerDetailResponse(
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
