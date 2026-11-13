using Ecommerce.Catalog.Contracts;
using Ecommerce.Identity.Contracts;
using Ecommerce.Inventory.Contracts;
using Ecommerce.Orders.Contracts;
using Ecommerce.SharedKernel.Projections;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Admin.Application;

internal sealed record DashboardSettings(string Currency);

internal sealed record StatusCount(string Status, int Count);

internal sealed record LowStockRow(Guid VariantId, string ProductName, string Sku, int Available);

internal sealed record LaggingProjector(string Name, int LagSeconds);

internal sealed record NeedsAttention(int PendingReturns, IReadOnlyList<ConsumerDeadLetters> DeadLetters, IReadOnlyList<LaggingProjector> LaggingProjectors);

internal sealed record RevenueComparison(decimal PreviousRevenue, decimal? ChangePercent);

internal sealed record DashboardView(
    string Period,
    DateTimeOffset AsOf,
    string Currency,
    DashboardKpis? Kpis,
    RevenueComparison? Comparison,
    IReadOnlyList<StatusCount> OrdersByStatus,
    IReadOnlyList<TopProduct> TopProducts,
    IReadOnlyList<LowStockRow> LowStock,
    int LowStockCount,
    int CancelledCount,
    int DeliveredCount,
    NeedsAttention NeedsAttention);

internal sealed record RecentOrderRow(Guid Id, string OrderNumber, string CustomerEmail, DateTimeOffset CreatedAt, decimal GrandTotal, string Currency, string Status);

internal sealed record RecentOrdersView(IReadOnlyList<RecentOrderRow> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// The dashboard (arch 8.8.5, Slice 6b D14, D19). KPIs, status counts and top products come from read models and carry an as-of time;
/// low stock, pending returns and recent orders are direct reads through the owning modules' contracts. Support never gets KPIs.
/// </summary>
internal sealed class DashboardQueryService(
    IDashboardStore store,
    IClock clock,
    DashboardSettings settings,
    IConsumerHealthReader health,
    IProductSalesReader sales,
    IInventoryReader inventory,
    IProductReader products,
    IOrderReader orders,
    IUserReader users)
{
    public const int TopCount = 5;
    public const int LowStockMax = 20;

    public async Task<DashboardView> GetAsync(DashboardPeriod period, bool includeKpis, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset from = period.From(now);
        IReadOnlyList<ProjectorHealth> projectors = await health.GetProjectorsAsync(ct);

        IReadOnlyList<StatusCount> statuses = await store.CountByStatusAsync(ct);
        IReadOnlyList<TopProduct> top = await sales.GetTopAsync(DateOnly.FromDateTime(from.UtcDateTime), DateOnly.FromDateTime(now.UtcDateTime), TopCount, ct);
        IReadOnlyList<LowStockRow> lowStock = await LowStockAsync(ct);

        List<ConsumerDeadLetters> dead = [.. await health.GetConsumerDeadLettersAsync(ct)];
        dead.AddRange(projectors.Where(p => p.DeadLetters > 0).Select(p => new ConsumerDeadLetters(p.Name, p.DeadLetters)));

        DashboardKpis? kpis = null;
        RevenueComparison? comparison = null;
        if (includeKpis)
        {
            kpis = await store.ReadKpisAsync(from, now, ct);
            decimal previous = (await store.ReadKpisAsync(period.PreviousFrom(now), period.PreviousTo(now), ct)).Revenue;
            comparison = new RevenueComparison(previous, KpiCalculator.ChangePercent(kpis.Revenue, previous));
        }

        return new DashboardView(
            period.Name,
            projectors.Count == 0 ? now : projectors.Min(p => p.AsOf),
            settings.Currency,
            kpis,
            comparison,
            statuses,
            top,
            lowStock,
            await inventory.CountLowStockAsync(ct),
            await orders.CountCancelledAsync(from, now, ct),
            await orders.CountDeliveredAsync(from, now, ct),
            new NeedsAttention(
                await orders.CountPendingReturnsAsync(ct),
                dead,
                projectors.Where(p => p.Lagging).Select(p => new LaggingProjector(p.Name, p.LagSeconds)).ToList()));
    }

    public async Task<RecentOrdersView> GetRecentAsync(int page, int pageSize, CancellationToken ct)
    {
        RecentOrderPage result = await orders.ListRecentAsync(page, pageSize, ct);
        IReadOnlyDictionary<Guid, UserContact> contacts = await users.GetContactsAsync(result.Items.Select(o => o.UserId).Distinct().ToList(), ct);
        return new RecentOrdersView(
            result.Items.Select(o => new RecentOrderRow(
                o.Id, o.OrderNumber, contacts.TryGetValue(o.UserId, out UserContact? c) ? c.Email : "", o.CreatedAt, o.GrandTotal, o.Currency, o.Status)).ToList(),
            page, pageSize, result.TotalCount);
    }


    private async Task<IReadOnlyList<LowStockRow>> LowStockAsync(CancellationToken ct)
    {
        IReadOnlyList<LowStockItem> low = await inventory.ListLowStockAsync(LowStockMax, ct);
        if (low.Count == 0)
        {
            return [];
        }
        IReadOnlyList<VariantRef> refs = await products.GetVariantsAsync(low.Select(l => l.VariantId).ToList(), ct);
        Dictionary<Guid, VariantRef> byId = refs.ToDictionary(r => r.VariantId);
        return low.Where(l => byId.ContainsKey(l.VariantId))
            .Select(l => new LowStockRow(l.VariantId, byId[l.VariantId].ProductName, byId[l.VariantId].Sku, l.Available)).ToList();
    }
}
