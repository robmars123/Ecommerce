using System.Text.Json;
using Ecommerce.Catalog.Contracts;
using Ecommerce.Catalog.Domain;
using Ecommerce.Orders.Contracts;
using Ecommerce.Persistence;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Projections;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure;

/// <summary>
/// The search projector (Slice 6b D15): units sold per product per UTC day, from OrderPaid events. It maps variant to product in
/// Catalog's own tables. Not versioned: OrderPaid is the only order event it consumes. The same table feeds best sellers and
/// the dashboard's top products through <see cref="IProductSalesReader"/>.
/// </summary>
internal sealed class CatalogSalesProjector(EcommerceDbContext db) : IProjector, IProductSalesReader
{
    public const string ProjectorName = "catalog.search";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record VariantProduct(Guid VariantId, Guid ProductId);

    private sealed record ProductUnits(Guid ProductId, int Units);

    public string Name => ProjectorName;

    public IReadOnlySet<string> EventTypes { get; } = new HashSet<string> { OrderEventTypes.OrderPaid };

    public IReadOnlySet<string> VersionedAggregateTypes { get; } = new HashSet<string>();

    public async Task ApplyAsync(OutboxMessage message, CancellationToken ct)
    {
        OrderPaidPayload paid = JsonSerializer.Deserialize<OrderPaidPayload>(message.PayloadJson, Json)!;
        Guid[] variantIds = [.. paid.Items.Select(i => i.VariantId)];
        Dictionary<Guid, Guid> productOf = (await db.Set<ProductVariant>().AsNoTracking()
            .Where(v => variantIds.Contains(v.Id)).Select(v => new VariantProduct(v.Id, v.ProductId)).ToListAsync(ct))
            .ToDictionary(v => v.VariantId, v => v.ProductId);
        DateOnly day = DateOnly.FromDateTime(paid.OccurredAt.UtcDateTime);

        foreach (IGrouping<Guid, OrderPaidLine> lines in paid.Items.Where(i => productOf.ContainsKey(i.VariantId)).GroupBy(i => productOf[i.VariantId]))
        {
            ProductSalesDaily? row = await db.Set<ProductSalesDaily>().FindAsync([lines.Key, day], ct);
            int units = lines.Sum(l => l.Quantity);
            if (row is null)
            {
                db.Set<ProductSalesDaily>().Add(new ProductSalesDaily { ProductId = lines.Key, SalesDate = day, Units = units });
            }
            else
            {
                row.Units += units;
            }
        }
    }

    public async Task ResetAsync(CancellationToken ct) => await db.Set<ProductSalesDaily>().ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<TopProduct>> GetTopAsync(DateOnly fromDate, DateOnly toDate, int count, CancellationToken ct)
    {
        List<ProductUnits> top = await db.Set<ProductSalesDaily>().AsNoTracking()
            .Where(s => s.SalesDate >= fromDate && s.SalesDate <= toDate)
            .GroupBy(s => s.ProductId)
            .OrderByDescending(g => g.Sum(s => s.Units)).ThenBy(g => g.Key)
            .Take(count)
            .Select(g => new ProductUnits(g.Key, g.Sum(s => s.Units)))
            .ToListAsync(ct);
        Guid[] ids = [.. top.Select(t => t.ProductId)];
        Dictionary<Guid, string> names = await db.Set<Product>().AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return top.Where(t => names.ContainsKey(t.ProductId)).Select(t => new TopProduct(t.ProductId, names[t.ProductId], t.Units)).ToList();
    }
}
