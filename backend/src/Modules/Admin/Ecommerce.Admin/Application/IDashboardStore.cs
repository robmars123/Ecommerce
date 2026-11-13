namespace Ecommerce.Admin.Application;

/// <summary>Admin's port to its own read model tables (arch 6.2: the Application layer never sees EF).</summary>
internal interface IDashboardStore
{
    Task<IReadOnlyList<StatusCount>> CountByStatusAsync(CancellationToken ct);

    /// <summary>Gross paid, refunds succeeded and customers over [from, to] (cash basis, Slice 6b D9).</summary>
    Task<DashboardKpis> ReadKpisAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);

    /// <summary>Paid times and actor refund times over [from, to], the same predicates as <see cref="ReadKpisAsync"/> (P2-tier4 D2).</summary>
    Task<RevenueEvents> ReadRevenueEventsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
