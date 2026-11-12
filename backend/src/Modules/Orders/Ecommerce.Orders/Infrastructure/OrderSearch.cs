using Ecommerce.Orders.Contracts;
using Ecommerce.Orders.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Orders.Infrastructure;

/// <summary>Admin search by order number: read-only, no tracking, any status.</summary>
internal sealed class OrderSearch(EcommerceDbContext db) : IOrderSearch
{
    private sealed record Row(Guid Id, string OrderNumber, OrderStatus Status);

    public async Task<IReadOnlyList<OrderSearchHit>> SearchByNumberAsync(string term, int max, CancellationToken ct)
    {
        string pattern = Contains(term);
        List<Row> rows = await db.Set<Order>().AsNoTracking()
            .Where(o => EF.Functions.ILike(o.OrderNumber, pattern, "\\"))
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Take(max)
            .Select(o => new Row(o.Id, o.OrderNumber, o.Status))
            .ToListAsync(ct);
        return rows.Select(r => new OrderSearchHit(r.Id, r.OrderNumber, r.Status.ToString())).ToList();
    }

    private static string Contains(string term) =>
        "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
