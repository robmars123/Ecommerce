using Ecommerce.Orders.Domain;

namespace Ecommerce.Orders.Application;

internal interface IOrderStore
{
    /// <summary>The tracked order with its shipments, or null.</summary>
    Task<Order?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Stages a new order with its items and history. The caller transaction saves it.</summary>
    void Add(Order order);

    /// <summary>The tracked order with its items, locked FOR UPDATE (needs an open transaction), or null.</summary>
    Task<Order?> LockForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The next value of the order number sequence (lock-free, gaps are fine).</summary>
    Task<long> NextOrderNumberAsync(CancellationToken ct);

    /// <summary>The owner's user id, or null when the order does not exist. No-tracking, no lock.</summary>
    Task<Guid?> GetOwnerAsync(Guid id, CancellationToken ct);
}

internal interface IOrderReadStore
{
    Task<OrderListRows> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct);

    /// <summary>Any user's order by id; the caller decides ownership.</summary>
    Task<OrderDetailRow?> GetAsync(Guid orderId, CancellationToken ct);
}

internal sealed record ReturnItemRow(Guid VariantId, string Name, int Quantity);

internal sealed record ReturnRow(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    ReturnStatus Status,
    string Reason,
    string? AdminNote,
    IReadOnlyList<ReturnItemRow> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt);

internal sealed record ReturnRows(IReadOnlyList<ReturnRow> Items, int TotalCount);

internal interface IReturnStore
{
    void Add(ReturnRequest request);

    /// <summary>The tracked request FOR UPDATE (needs an open transaction), or null.</summary>
    Task<ReturnRequest?> LockAsync(Guid id, CancellationToken ct);

    /// <summary>The order's open (Requested or Approved) request, tracked, or null. At most one exists (partial unique index).</summary>
    Task<ReturnRequest?> GetOpenAsync(Guid orderId, CancellationToken ct);

    Task<ReturnRows> ListAsync(ReturnStatus? status, int page, int pageSize, CancellationToken ct);

    Task<IReadOnlyList<ReturnRow>> ListForOrderAsync(Guid orderId, CancellationToken ct);

    Task<ReturnRow?> GetRowAsync(Guid id, CancellationToken ct);
}
