using Ecommerce.Admin.Application;
using Ecommerce.Admin.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Admin.Infrastructure;

/// <summary>Reads the dashboard read model: Admin's own tables only (no cross-module DbSet or join).</summary>
internal sealed class DashboardStore(EcommerceDbContext db) : IDashboardStore
{
    public async Task<IReadOnlyList<StatusCount>> CountByStatusAsync(CancellationToken ct) =>
        await db.Set<DashboardOrder>().AsNoTracking()
            .GroupBy(o => o.Status).OrderBy(g => g.Key).Select(g => new StatusCount(g.Key, g.Count())).ToListAsync(ct);

    public async Task<DashboardKpis> ReadKpisAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        List<PaidOrderRow> paid = await db.Set<DashboardOrder>().AsNoTracking()
            .Where(o => o.PaidAt >= from && o.PaidAt <= to)
            .Select(o => new PaidOrderRow(o.UserId, o.GrandTotal!.Value)).ToListAsync(ct);
        decimal refunded = await db.Set<DashboardRefund>().AsNoTracking()
            .Where(r => r.SucceededAt >= from && r.SucceededAt <= to).SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        Guid[] buyers = [.. paid.Select(p => p.UserId).Distinct()];
        HashSet<Guid> before = (await db.Set<DashboardOrder>().AsNoTracking()
            .Where(o => o.PaidAt < from && buyers.Contains(o.UserId)).Select(o => o.UserId).Distinct().ToListAsync(ct)).ToHashSet();
        return KpiCalculator.Compute(paid, refunded, before);
    }
}
