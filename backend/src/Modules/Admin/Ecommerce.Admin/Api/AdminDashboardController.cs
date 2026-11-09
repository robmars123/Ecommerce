using System.Globalization;
using Ecommerce.Admin.Application;
using Ecommerce.Identity.Contracts;
using Ecommerce.SharedKernel.Projections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Admin.Api;

internal sealed record MoneyResponse(string Amount, string Currency)
{
    public static MoneyResponse From(decimal amount, string currency) =>
        new(amount.ToString("0.00##", CultureInfo.InvariantCulture), currency);
}

internal sealed record CustomersResponse(int Total, int New, int Returning);

internal sealed record KpisResponse(MoneyResponse Revenue, int OrderCount, MoneyResponse AverageOrderValue, CustomersResponse Customers);

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
    NeedsAttentionResponse NeedsAttention);

internal sealed record RecentOrderResponse(
    Guid Id, string OrderNumber, string CustomerEmail, DateTimeOffset CreatedAt, MoneyResponse GrandTotal, string Status);

internal sealed record RecentOrdersResponse(IReadOnlyList<RecentOrderResponse> Items, int Page, int PageSize, int TotalCount);

/// <summary>Admin and Support (Slice 6b D19). Thin: validate the query, call the service, map. Money is a string with its currency (rule 1).</summary>
[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Policy = AuthorizationPolicies.AdminOrSupport)]
internal sealed class AdminDashboardController(DashboardQueryService dashboard) : ControllerBase
{
    private const int MaxPageSize = 50;

    [HttpGet("")]
    public async Task<ActionResult<DashboardResponse>> Get([FromQuery] string? period, CancellationToken ct)
    {
        DashboardPeriod? parsed = DashboardPeriod.Parse(period);
        if (parsed is null)
        {
            ModelState.AddModelError("period", "period must be one of: today, 7d, 30d.");
            return ValidationProblem(ModelState);
        }

        // Support never sees money figures (D19): the guard is the role, not the client.
        DashboardView view = await dashboard.GetAsync(parsed, User.IsInRole(IdentityRoles.Admin), ct);
        string currency = view.Currency;
        return Ok(new DashboardResponse(
            view.Period, view.AsOf, currency,
            view.Kpis is null
                ? null
                : new KpisResponse(
                    MoneyResponse.From(view.Kpis.Revenue, currency), view.Kpis.OrderCount, MoneyResponse.From(view.Kpis.AverageOrderValue, currency),
                    new CustomersResponse(view.Kpis.Customers, view.Kpis.NewCustomers, view.Kpis.ReturningCustomers)),
            view.OrdersByStatus,
            view.TopProducts.Select(t => new TopProductResponse(t.ProductId, t.Name, t.Units)).ToList(),
            view.LowStock.Select(l => new LowStockResponse(l.VariantId, l.ProductName, l.Sku, l.Available)).ToList(),
            new NeedsAttentionResponse(
                view.NeedsAttention.PendingReturns,
                view.NeedsAttention.DeadLetters.Select(d => new DeadLetterResponse(d.Consumer, d.Count)).ToList(),
                view.NeedsAttention.LaggingProjectors.Select(p => new LaggingProjectorResponse(p.Name, p.LagSeconds)).ToList())));
    }

    [HttpGet("recent-orders")]
    public async Task<ActionResult<RecentOrdersResponse>> RecentOrders([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        if (page is < 1)
        {
            ModelState.AddModelError("page", "page must be 1 or greater.");
        }
        if (pageSize is < 1 or > MaxPageSize)
        {
            ModelState.AddModelError("pageSize", $"pageSize must be between 1 and {MaxPageSize}.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        RecentOrdersView view = await dashboard.GetRecentAsync(page ?? 1, pageSize ?? 10, ct);
        return Ok(new RecentOrdersResponse(
            view.Items.Select(o => new RecentOrderResponse(o.Id, o.OrderNumber, o.CustomerEmail, o.CreatedAt, MoneyResponse.From(o.GrandTotal, o.Currency), o.Status)).ToList(),
            view.Page, view.PageSize, view.TotalCount));
    }
}

/// <summary>Admin only (Slice 6b D5). Replay rebuilds one projector from the retained outbox; 409 while another worker holds it.</summary>
[ApiController]
[Route("api/v1/admin/projections")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
internal sealed class AdminProjectionsController(IProjectionReplayer replayer) : ControllerBase
{
    [HttpPost("{name}/replay")]
    public async Task<IActionResult> Replay(string name, CancellationToken ct) =>
        await replayer.ReplayAsync(name, ct) switch
        {
            ReplayOutcome.Done => Accepted(),
            ReplayOutcome.Busy => Problem(statusCode: StatusCodes.Status409Conflict, title: "Projection busy", detail: "projection-busy: another worker is running this projector."),
            _ => Problem(statusCode: StatusCodes.Status404NotFound, title: "Projection not found"),
        };
}
