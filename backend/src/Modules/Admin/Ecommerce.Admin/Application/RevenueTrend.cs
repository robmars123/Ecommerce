namespace Ecommerce.Admin.Application;

internal sealed record TrendBucket(DateTimeOffset Start, decimal Revenue, int Orders);

internal sealed record TrendSeries(IReadOnlyList<TrendBucket> Buckets, decimal Total, int OrderCount);

internal sealed record RevenueTrendView(
    string Period, string Granularity, DateTimeOffset AsOf, DateTimeOffset To, string Currency, decimal Total, int OrderCount, IReadOnlyList<TrendBucket> Buckets);

internal sealed record PaidAtRow(DateTimeOffset PaidAt, decimal GrandTotal);

internal sealed record RefundAtRow(DateTimeOffset SucceededAt, decimal Amount);

internal sealed record RevenueEvents(IReadOnlyList<PaidAtRow> Paid, IReadOnlyList<RefundAtRow> Refunds);

/// <summary>
/// Revenue per bucket (P2-tier4 D1, D2): paid in the bucket minus actor refunds succeeded in it, over the same window as the KPIs
/// ([from, now]), so the buckets add up to the dashboard revenue. Today is hourly, 7d and 30d are daily; there are no future buckets.
/// </summary>
internal static class TrendCalculator
{
    public static TrendSeries Compute(DashboardPeriod period, DateTimeOffset now, RevenueEvents events)
    {
        DateTimeOffset from = period.From(now);
        bool hourly = period.Days == 1;
        int count = hourly ? now.UtcDateTime.Hour + 1 : period.Days;
        TimeSpan step = hourly ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
        decimal[] revenue = new decimal[count];
        int[] orders = new int[count];

        foreach (PaidAtRow row in events.Paid.Where(p => p.PaidAt >= from && p.PaidAt <= now))
        {
            int index = (int)((row.PaidAt - from) / step);
            revenue[index] += row.GrandTotal;
            orders[index]++;
        }
        foreach (RefundAtRow row in events.Refunds.Where(r => r.SucceededAt >= from && r.SucceededAt <= now))
        {
            revenue[(int)((row.SucceededAt - from) / step)] -= row.Amount;
        }

        List<TrendBucket> buckets = [.. Enumerable.Range(0, count).Select(i => new TrendBucket(from + (step * i), revenue[i], orders[i]))];
        return new TrendSeries(buckets, revenue.Sum(), orders.Sum());
    }
}
