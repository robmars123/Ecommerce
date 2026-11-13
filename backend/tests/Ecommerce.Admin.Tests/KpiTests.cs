using Ecommerce.Admin.Application;

namespace Ecommerce.Admin.Tests;

// The dashboard arithmetic (Slice 6b D9, D10): cash-basis revenue, new versus returning customers, UTC period bounds.
public class KpiTests
{
    private static readonly Guid Ann = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly IReadOnlySet<Guid> NoOne = new HashSet<Guid>();

    [Fact]
    public void Revenue_is_the_gross_paid_minus_the_refunds_that_succeeded_in_the_period()
    {
        DashboardKpis kpis = KpiCalculator.Compute([new PaidOrderRow(Ann, 25.00m), new PaidOrderRow(Bob, 15.50m)], 5.25m, NoOne);

        Assert.Equal(35.25m, kpis.Revenue);
        Assert.Equal(2, kpis.OrderCount);
    }

    [Fact]
    public void The_average_order_value_is_the_gross_over_the_count_rounded_to_cents_and_zero_with_no_orders()
    {
        Assert.Equal(13.34m, KpiCalculator.Compute([new PaidOrderRow(Ann, 10m), new PaidOrderRow(Bob, 10m), new PaidOrderRow(Bob, 20.01m)], 0m, NoOne).AverageOrderValue);
        DashboardKpis none = KpiCalculator.Compute([], 0m, NoOne);
        Assert.Equal((0m, 0, 0m, 0), (none.Revenue, none.OrderCount, none.AverageOrderValue, none.Customers));
    }

    [Fact]
    public void A_refund_in_a_period_with_no_orders_makes_revenue_negative_not_hidden()
    {
        Assert.Equal(-4m, KpiCalculator.Compute([], 4m, NoOne).Revenue);
    }

    [Fact]
    public void A_customer_is_counted_once_and_is_returning_only_when_they_paid_before_the_period()
    {
        DashboardKpis kpis = KpiCalculator.Compute(
            [new PaidOrderRow(Ann, 10m), new PaidOrderRow(Ann, 12m), new PaidOrderRow(Bob, 9m)], 0m, new HashSet<Guid> { Bob });

        Assert.Equal((2, 1, 1), (kpis.Customers, kpis.NewCustomers, kpis.ReturningCustomers));
    }

    [Theory]
    [InlineData("today", 1, "2026-11-08T00:00:00Z")]
    [InlineData("7d", 7, "2026-11-02T00:00:00Z")]
    [InlineData("30d", 30, "2026-10-10T00:00:00Z")]
    public void A_period_starts_at_UTC_midnight_of_today_minus_days_minus_one(string name, int days, string from)
    {
        DashboardPeriod? period = DashboardPeriod.Parse(name);

        Assert.Equal(new DashboardPeriod(name, days), period);
        Assert.Equal(DateTimeOffset.Parse(from), period!.From(DateTimeOffset.Parse("2026-11-08T23:59:59Z")));
        Assert.Equal(DateTimeOffset.Parse(from), period.From(DateTimeOffset.Parse("2026-11-08T00:00:00Z")));
    }

    [Fact]
    public void A_missing_period_is_today_and_anything_else_is_rejected()
    {
        Assert.Equal(DashboardPeriod.Today, DashboardPeriod.Parse(null));
        Assert.Equal(DashboardPeriod.Today, DashboardPeriod.Parse(""));
        Assert.Null(DashboardPeriod.Parse("90d"));
        Assert.Null(DashboardPeriod.Parse("Today"));
    }

    [Fact]
    public void The_revenue_change_is_the_percent_against_the_previous_rounded_to_one_place_away_from_zero()
    {
        Assert.Equal(20.0m, KpiCalculator.ChangePercent(120m, 100m));
        Assert.Equal(-16.7m, KpiCalculator.ChangePercent(100m, 120m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public void The_revenue_change_is_null_when_the_previous_revenue_is_zero(int current)
    {
        Assert.Null(KpiCalculator.ChangePercent(current, 0m));
    }

    [Fact]
    public void The_revenue_change_divides_by_the_absolute_previous_so_the_sign_means_up_or_down()
    {
        Assert.Equal(50.0m, KpiCalculator.ChangePercent(-50m, -100m));
        Assert.Equal(150.0m, KpiCalculator.ChangePercent(50m, -100m));
    }

    [Theory]
    [InlineData("today", "2026-11-11T00:00:00Z", "2026-11-11T14:05:00Z")]
    [InlineData("7d", "2026-10-30T00:00:00Z", "2026-11-05T14:05:00Z")]
    [InlineData("30d", "2026-09-14T00:00:00Z", "2026-10-13T14:05:00Z")]
    public void The_previous_window_is_the_same_elapsed_time_one_period_earlier_and_never_overlaps(string name, string from, string to)
    {
        DashboardPeriod period = DashboardPeriod.Parse(name)!;
        DateTimeOffset now = DateTimeOffset.Parse("2026-11-12T14:05:00Z");

        Assert.Equal(DateTimeOffset.Parse(from), period.PreviousFrom(now));
        Assert.Equal(DateTimeOffset.Parse(to), period.PreviousTo(now));
        Assert.True(period.PreviousTo(now) < period.From(now));
    }

    [Fact]
    public void The_period_start_ignores_the_offset_of_the_clock_value()
    {
        DashboardPeriod today = DashboardPeriod.Today;

        Assert.Equal(DateTimeOffset.Parse("2026-11-08T00:00:00Z"), today.From(DateTimeOffset.Parse("2026-11-08T01:30:00+05:00")).ToUniversalTime().AddDays(1));
    }
}
