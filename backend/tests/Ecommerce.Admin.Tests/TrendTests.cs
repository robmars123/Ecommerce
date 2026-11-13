using System.Globalization;
using Ecommerce.Admin.Api;
using Ecommerce.Admin.Application;

namespace Ecommerce.Admin.Tests;

// P2-tier4 revenue trend (D1, D2, D5): bucket boundaries, refund subtraction, the CSV.
public class TrendTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 14, 20, 0, TimeSpan.Zero);

    private static TrendSeries Compute(DashboardPeriod period, IReadOnlyList<PaidAtRow> paid, IReadOnlyList<RefundAtRow>? refunds = null) =>
        TrendCalculator.Compute(period, Now, new RevenueEvents(paid, refunds ?? []));

    [Fact]
    public void Today_has_hourly_buckets_from_midnight_to_the_current_hour_and_7d_and_30d_have_daily_ones()
    {
        TrendSeries today = Compute(DashboardPeriod.Today, []);
        Assert.Equal(15, today.Buckets.Count);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero), today.Buckets[0].Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 14, 0, 0, TimeSpan.Zero), today.Buckets[^1].Start);

        foreach (string name in new[] { "7d", "30d" })
        {
            DashboardPeriod period = DashboardPeriod.Parse(name)!;
            TrendSeries series = Compute(period, []);
            Assert.Equal(period.Days, series.Buckets.Count);
            Assert.Equal(period.From(Now), series.Buckets[0].Start);
            Assert.Equal(TimeSpan.FromDays(1), series.Buckets[1].Start - series.Buckets[0].Start);
        }
    }

    [Fact]
    public void A_payment_exactly_on_a_boundary_lands_in_the_later_bucket_one_at_now_in_the_last_and_rows_outside_the_window_are_ignored()
    {
        DateTimeOffset boundary = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
        TrendSeries series = Compute(DashboardPeriod.Today, [
            new PaidAtRow(boundary, 10m),
            new PaidAtRow(Now, 20m),
            new PaidAtRow(new DateTimeOffset(2026, 11, 11, 23, 59, 59, TimeSpan.Zero), 99m),
            new PaidAtRow(Now.AddSeconds(1), 98m)]);

        Assert.Equal(0m, series.Buckets[8].Revenue);
        Assert.Equal(10m, series.Buckets[9].Revenue);
        Assert.Equal(20m, series.Buckets[14].Revenue);
        Assert.Equal(2, series.OrderCount);
        Assert.Equal(30m, series.Total);
    }

    [Fact]
    public void An_actor_refund_lowers_its_own_bucket_only_a_bucket_can_go_negative_and_the_total_is_paid_minus_refunds()
    {
        DashboardPeriod week = DashboardPeriod.Parse("7d")!;
        DateTimeOffset day2 = week.From(Now).AddDays(2).AddHours(3);
        TrendSeries series = Compute(week,
            [new PaidAtRow(week.From(Now).AddHours(5), 25m)],
            [new RefundAtRow(day2, 4m), new RefundAtRow(week.From(Now).AddDays(-1), 50m)]);

        Assert.Equal(25m, series.Buckets[0].Revenue);
        Assert.Equal(-4m, series.Buckets[2].Revenue);
        Assert.Equal(21m, series.Total);
        Assert.Equal(1, series.OrderCount);
        Assert.Equal(series.Total, series.Buckets.Sum(b => b.Revenue));
    }

    [Fact]
    public void With_no_rows_every_bucket_is_zero_and_the_total_is_zero()
    {
        TrendSeries series = Compute(DashboardPeriod.Parse("30d")!, []);

        Assert.All(series.Buckets, b => Assert.Equal((0m, 0), (b.Revenue, b.Orders)));
        Assert.Equal((0m, 0), (series.Total, series.OrderCount));
    }

    [Fact]
    public void Granularity_is_hour_for_today_and_day_for_the_others()
    {
        Assert.Equal("hour", DashboardPeriod.Today.Granularity);
        Assert.Equal("day", DashboardPeriod.Parse("7d")!.Granularity);
        Assert.Equal("day", DashboardPeriod.Parse("30d")!.Granularity);
    }

    [Fact]
    public void The_csv_has_a_header_crlf_ends_invariant_amounts_under_de_DE_Z_starts_and_one_row_per_bucket()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            TrendSeries series = Compute(DashboardPeriod.Parse("7d")!, [new PaidAtRow(DashboardPeriod.Parse("7d")!.From(Now).AddHours(1), 1234.5m)]);
            RevenueTrendView view = new("7d", "day", Now, Now, "USD", series.Total, series.OrderCount, series.Buckets);

            string csv = TrendCsv.Write(view);

            string[] lines = csv.Split("\r\n");
            Assert.Equal("bucket_start_utc,revenue,orders,currency", lines[0]);
            Assert.Equal("2026-11-06T00:00:00Z,1234.50,1,USD", lines[1]);
            Assert.Equal("2026-11-07T00:00:00Z,0.00,0,USD", lines[2]);
            Assert.Equal(9, lines.Length);
            Assert.Equal("", lines[^1]);
            Assert.DoesNotContain("\n", csv.Replace("\r\n", ""));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
