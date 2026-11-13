using Ecommerce.SharedKernel.Projections;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Admin.Application;

internal sealed class RevenueTrendService(IDashboardStore store, IClock clock, DashboardSettings settings, IConsumerHealthReader health)
{
    public async Task<RevenueTrendView> GetAsync(DashboardPeriod period, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        IReadOnlyList<ProjectorHealth> projectors = await health.GetProjectorsAsync(ct);
        RevenueEvents events = await store.ReadRevenueEventsAsync(period.From(now), now, ct);
        TrendSeries series = TrendCalculator.Compute(period, now, events);
        return new RevenueTrendView(period.Name, period.Granularity, ProjectionAsOf.From(projectors, now), now, settings.Currency, series.Total, series.OrderCount, series.Buckets);
    }
}
