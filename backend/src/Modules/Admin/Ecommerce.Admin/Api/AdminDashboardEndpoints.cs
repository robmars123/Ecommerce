using System.Globalization;
using System.Security.Claims;
using Ecommerce.Admin.Application;
using Ecommerce.Identity.Contracts;
using Ecommerce.SharedKernel.Projections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Admin.Api;

internal sealed record MoneyResponse(string Amount, string Currency)
{
    public static MoneyResponse From(decimal amount, string currency) =>
        new(amount.ToString("0.00##", CultureInfo.InvariantCulture), currency);
}

internal sealed record CustomersResponse(int Total, int New, int Returning);

internal sealed record KpisResponse(MoneyResponse Revenue, int OrderCount, MoneyResponse AverageOrderValue, CustomersResponse Customers,
    MoneyResponse PreviousRevenue, decimal? RevenueChangePercent);

internal sealed record TopProductResponse(Guid ProductId, string Name, int Units);

internal sealed record LowStockResponse(Guid VariantId, string ProductName, string Sku, int Available);

internal sealed record DeadLetterResponse(string Consumer, int Count);

internal sealed record LaggingProjectorResponse(string Name, int LagSeconds);

internal sealed record NeedsAttentionResponse(
    int PendingReturns, IReadOnlyList<DeadLetterResponse> DeadLetters, IReadOnlyList<LaggingProjectorResponse> LaggingProjectors);

internal sealed record DashboardResponse(
    string Period,
    DateTimeOffset AsOf,
    string Currency,
    KpisResponse? Kpis,
    IReadOnlyList<StatusCount> OrdersByStatus,
    IReadOnlyList<TopProductResponse> TopProducts,
    IReadOnlyList<LowStockResponse> LowStock,
    int LowStockCount,
    int CancelledCount,
    int DeliveredCount,
    NeedsAttentionResponse NeedsAttention);

internal sealed record RecentOrderResponse(
    Guid Id, string OrderNumber, string CustomerEmail, DateTimeOffset CreatedAt, MoneyResponse GrandTotal, string Status);

internal sealed record RecentOrdersResponse(IReadOnlyList<RecentOrderResponse> Items, int Page, int PageSize, int TotalCount);

/// <summary>Admin and Support (Slice 6b D19). Thin: validate the query, call the service, map. Money is a string with its currency (rule 1).</summary>
internal static class AdminDashboardEndpoints
{
    private const int MaxPageSize = 50;

    public static void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.AdminOrSupport);
        group.MapGet("dashboard", Get);
        group.MapGet("dashboard/recent-orders", RecentOrders);
    }

    // The query values are string[] so a repeated key uses the first value, as MVC did (a plain string would join them as "a,b").
    private static async Task<Results<Ok<DashboardResponse>, ValidationProblem>> Get(
        [FromQuery] string[]? period, ClaimsPrincipal user, [FromServices] DashboardQueryService dashboard, CancellationToken ct)
    {
        string? raw = period?.FirstOrDefault();
        DashboardPeriod? parsed = DashboardPeriod.Parse(string.IsNullOrWhiteSpace(raw) ? null : raw);
        if (parsed is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["period"] = ["period must be one of: today, 7d, 30d."] });
        }

        // Support never sees money figures (D19): the guard is the role, not the client.
        DashboardView view = await dashboard.GetAsync(parsed, user.IsInRole(IdentityRoles.Admin), ct);
        string currency = view.Currency;
        return TypedResults.Ok(new DashboardResponse(
            view.Period, view.AsOf, currency,
            view.Kpis is null
                ? null
                : new KpisResponse(
                    MoneyResponse.From(view.Kpis.Revenue, currency), view.Kpis.OrderCount, MoneyResponse.From(view.Kpis.AverageOrderValue, currency),
                    new CustomersResponse(view.Kpis.Customers, view.Kpis.NewCustomers, view.Kpis.ReturningCustomers),
                    MoneyResponse.From(view.Comparison!.PreviousRevenue, currency), view.Comparison.ChangePercent),
            view.OrdersByStatus,
            view.TopProducts.Select(t => new TopProductResponse(t.ProductId, t.Name, t.Units)).ToList(),
            view.LowStock.Select(l => new LowStockResponse(l.VariantId, l.ProductName, l.Sku, l.Available)).ToList(),
            view.LowStockCount,
            view.CancelledCount,
            view.DeliveredCount,
            new NeedsAttentionResponse(
                view.NeedsAttention.PendingReturns,
                view.NeedsAttention.DeadLetters.Select(d => new DeadLetterResponse(d.Consumer, d.Count)).ToList(),
                view.NeedsAttention.LaggingProjectors.Select(p => new LaggingProjectorResponse(p.Name, p.LagSeconds)).ToList())));
    }

    private static async Task<Results<Ok<RecentOrdersResponse>, ValidationProblem>> RecentOrders(
        [AsParameters] RecentOrdersQuery query,
        [FromServices] DashboardQueryService dashboard,
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

        if (pageNumber is < 1)
        {
            errors["page"] = ["page must be 1 or greater."];
        }
        if (pageSizeNumber is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"pageSize must be between 1 and {MaxPageSize}."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        RecentOrdersView view = await dashboard.GetRecentAsync(pageNumber ?? 1, pageSizeNumber ?? 10, ct);
        return TypedResults.Ok(new RecentOrdersResponse(
            view.Items.Select(o => new RecentOrderResponse(o.Id, o.OrderNumber, o.CustomerEmail, o.CreatedAt, MoneyResponse.From(o.GrandTotal, o.Currency), o.Status)).ToList(),
            view.Page, view.PageSize, view.TotalCount));
    }
}
