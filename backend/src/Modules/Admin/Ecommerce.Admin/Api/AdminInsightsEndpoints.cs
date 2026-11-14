using System.Globalization;
using System.Text;
using Ecommerce.Admin.Application;
using Ecommerce.Identity.Contracts;
using Ecommerce.SharedKernel.Time;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Admin.Api;

internal sealed record TrendBucketResponse(DateTimeOffset Start, MoneyResponse Revenue, int Orders);

internal sealed record RevenueTrendResponse(
    string Period, string Granularity, DateTimeOffset AsOf, string Currency, MoneyResponse Total, int OrderCount, IReadOnlyList<TrendBucketResponse> Buckets);

/// <summary>Admin only (P2-tier4 D6): revenue over time is money, which Support never sees. Its own group, not Dashboard's AdminOrSupport one.</summary>
internal static class AdminInsightsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("dashboard/trend", Trend);
        group.MapGet("dashboard/export", Export);
    }

    private static async Task<Results<Ok<RevenueTrendResponse>, ValidationProblem>> Trend(
        [FromQuery] string[]? period, [FromServices] RevenueTrendService trend, CancellationToken ct)
    {
        DashboardPeriod? parsed = ReadPeriod(period);
        if (parsed is null)
        {
            return InvalidPeriod();
        }

        RevenueTrendView view = await trend.GetAsync(parsed, ct);
        return TypedResults.Ok(new RevenueTrendResponse(
            view.Period, view.Granularity, view.AsOf, view.Currency, MoneyResponse.From(view.Total, view.Currency), view.OrderCount,
            view.Buckets.Select(b => new TrendBucketResponse(b.Start, MoneyResponse.From(b.Revenue, view.Currency), b.Orders)).ToList()));
    }

    private static async Task<Results<FileContentHttpResult, ValidationProblem>> Export(
        [FromQuery] string[]? period, [FromServices] RevenueTrendService trend, [FromServices] IClock clock, CancellationToken ct)
    {
        DashboardPeriod? parsed = ReadPeriod(period);
        if (parsed is null)
        {
            return InvalidPeriod();
        }

        RevenueTrendView view = await trend.GetAsync(parsed, ct);
        string fileName = $"dashboard-{view.Period}-{clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv";
        return TypedResults.File(Encoding.UTF8.GetBytes(TrendCsv.Write(view)), "text/csv; charset=utf-8", fileName);
    }

    // The query value is string[] so a repeated key uses the first value, as MVC did (a plain string would join them as "a,b").
    private static DashboardPeriod? ReadPeriod(string[]? period)
    {
        string? raw = period?.FirstOrDefault();
        return DashboardPeriod.Parse(string.IsNullOrWhiteSpace(raw) ? null : raw);
    }

    private static ValidationProblem InvalidPeriod() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["period"] = ["period must be one of: today, 7d, 30d."] });
}
