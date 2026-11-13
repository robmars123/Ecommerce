namespace Ecommerce.Admin.Application;

/// <summary>Today, 7d or 30d as UTC dates (Slice 6b D10): from 00:00 UTC of today, today minus 6 or today minus 29, up to now.</summary>
internal sealed record DashboardPeriod(string Name, int Days)
{
    public static readonly DashboardPeriod Today = new("today", 1);

    public static DashboardPeriod? Parse(string? value) => value switch
    {
        null or "" or "today" => Today,
        "7d" => new DashboardPeriod("7d", 7),
        "30d" => new DashboardPeriod("30d", 30),
        _ => null,
    };

    public string Granularity => Days == 1 ? "hour" : "day";

    public DateTimeOffset From(DateTimeOffset now) => new(now.UtcDateTime.Date.AddDays(1 - Days), TimeSpan.Zero);

    /// <summary>The previous window has the same elapsed time one period earlier, so it never overlaps the current one (P2.22 D1).</summary>
    public DateTimeOffset PreviousFrom(DateTimeOffset now) => From(now).AddDays(-Days);

    public DateTimeOffset PreviousTo(DateTimeOffset now) => now.AddDays(-Days);
}

internal sealed record PaidOrderRow(Guid UserId, decimal GrandTotal);

internal sealed record DashboardKpis(
    decimal Revenue, int OrderCount, decimal AverageOrderValue, int Customers, int NewCustomers, int ReturningCustomers);

/// <summary>
/// The KPI arithmetic (Slice 6b D9). Revenue is cash basis: what was paid in the period minus the refunds that succeeded in it.
/// Order count and average order value are over the gross paid. A customer is new when the period holds their first paid order.
/// </summary>
internal static class KpiCalculator
{
    /// <summary>Percent change against the previous revenue, one place, away from zero. The absolute previous keeps the sign meaning up or down; null when there is nothing to compare.</summary>
    public static decimal? ChangePercent(decimal current, decimal previous) =>
        previous == 0m ? null : decimal.Round((current - previous) / Math.Abs(previous) * 100m, 1, MidpointRounding.AwayFromZero);

    public static DashboardKpis Compute(IReadOnlyList<PaidOrderRow> paidInPeriod, decimal refundedInPeriod, IReadOnlySet<Guid> usersPaidBeforePeriod)
    {
        decimal gross = paidInPeriod.Sum(o => o.GrandTotal);
        int count = paidInPeriod.Count;
        List<Guid> customers = paidInPeriod.Select(o => o.UserId).Distinct().ToList();
        int returning = customers.Count(usersPaidBeforePeriod.Contains);
        return new DashboardKpis(
            gross - refundedInPeriod, count, count == 0 ? 0m : decimal.Round(gross / count, 2, MidpointRounding.AwayFromZero),
            customers.Count, customers.Count - returning, returning);
    }
}
