namespace Ecommerce.Admin.Application;

/// <summary>Admin's port to its own read model tables (arch 6.2: the Application layer never sees EF).</summary>
internal interface IDashboardStore
{
    Task<IReadOnlyList<StatusCount>> CountByStatusAsync(CancellationToken ct);

    /// <summary>Gross paid, refunds succeeded and customers over [from, to] (cash basis, Slice 6b D9).</summary>
    Task<DashboardKpis> ReadKpisAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
