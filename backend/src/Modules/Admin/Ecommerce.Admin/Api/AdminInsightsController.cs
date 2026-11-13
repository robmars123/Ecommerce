using System.Globalization;
using System.Text;
using Ecommerce.Admin.Application;
using Ecommerce.Identity.Contracts;
using Ecommerce.SharedKernel.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Admin.Api;

internal sealed record TrendBucketResponse(DateTimeOffset Start, MoneyResponse Revenue, int Orders);

internal sealed record RevenueTrendResponse(
    string Period, string Granularity, DateTimeOffset AsOf, string Currency, MoneyResponse Total, int OrderCount, IReadOnlyList<TrendBucketResponse> Buckets);

/// <summary>Admin only, by the class policy (P2-tier4 D6): revenue over time is money, which Support never sees.</summary>
[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
internal sealed class AdminInsightsController(RevenueTrendService trend, IClock clock) : ControllerBase
{
    [HttpGet("trend")]
    public async Task<ActionResult<RevenueTrendResponse>> Trend([FromQuery] string? period, CancellationToken ct)
    {
        DashboardPeriod? parsed = DashboardPeriod.Parse(period);
        if (parsed is null)
        {
            return InvalidPeriod();
        }

        RevenueTrendView view = await trend.GetAsync(parsed, ct);
        return Ok(new RevenueTrendResponse(
            view.Period, view.Granularity, view.AsOf, view.Currency, MoneyResponse.From(view.Total, view.Currency), view.OrderCount,
            view.Buckets.Select(b => new TrendBucketResponse(b.Start, MoneyResponse.From(b.Revenue, view.Currency), b.Orders)).ToList()));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? period, CancellationToken ct)
    {
        DashboardPeriod? parsed = DashboardPeriod.Parse(period);
        if (parsed is null)
        {
            return InvalidPeriod();
        }

        RevenueTrendView view = await trend.GetAsync(parsed, ct);
        string fileName = $"dashboard-{view.Period}-{clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv";
        return File(Encoding.UTF8.GetBytes(TrendCsv.Write(view)), "text/csv; charset=utf-8", fileName);
    }

    private ActionResult InvalidPeriod()
    {
        ModelState.AddModelError("period", "period must be one of: today, 7d, 30d.");
        return ValidationProblem(ModelState);
    }
}
