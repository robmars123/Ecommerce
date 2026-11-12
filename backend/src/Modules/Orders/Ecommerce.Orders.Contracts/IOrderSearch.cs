namespace Ecommerce.Orders.Contracts;

public sealed record OrderSearchHit(Guid Id, string OrderNumber, string Status);

public interface IOrderSearch
{
    /// <summary>Case-insensitive substring on the order number, any status, newest first, at most <paramref name="max"/>.</summary>
    Task<IReadOnlyList<OrderSearchHit>> SearchByNumberAsync(string term, int max, CancellationToken ct);
}
