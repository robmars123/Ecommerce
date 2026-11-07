using System.Text.Json;
using Ecommerce.Catalog.Contracts;
using Ecommerce.Inventory.Contracts;
using Ecommerce.Inventory.Domain;
using Ecommerce.SharedKernel.Audit;
using Ecommerce.SharedKernel.Domain;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Inventory.Application;

internal enum AdjustOutcome
{
    Ok,
    NotFound,
    Stale,
    Insufficient,
}

internal sealed record InventoryItem(
    Guid VariantId, string ProductName, string Sku, string VariantName, bool IsActive,
    int OnHand, int Reserved, int Available, StockStatus StockStatus);

internal sealed record InventoryPage(IReadOnlyList<InventoryItem> Items, int Page, int PageSize, int TotalCount, int TotalPages);

/// <summary>Item is set only for Ok. OnHand and Reserved are the row's current values (for the 409 body).</summary>
internal sealed record AdjustResult(AdjustOutcome Outcome, InventoryItem? Item, int OnHand, int Reserved)
{
    public int Available => OnHand - Reserved;
}

/// <summary>
/// Admin stock list and adjust (arch 8.2, 8.7; D6, D7). Adjust runs in one transaction: lock the row, check
/// expectedOnHand, change, audit. A refused adjust still commits its audit row (nothing else changes).
/// </summary>
internal sealed class InventoryAdminService(
    IInventoryStore store,
    IProductReader products,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    IClock clock,
    StockSettings settings)
{
    private const string EntityType = "InventoryStock";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record StockState(int OnHand, int Reserved);

    private sealed record RejectedState(int OnHand, int Reserved, int AttemptedDelta);

    public async Task<InventoryPage> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        VariantRefPage refs = await products.ListVariantsAsync(page, pageSize, ct);
        IReadOnlyDictionary<Guid, StockCounts> counts = await store.GetAsync(refs.Items.Select(v => v.VariantId).ToList(), ct);
        List<InventoryItem> items = refs.Items.Select(v => ToItem(v, counts.GetValueOrDefault(v.VariantId, new StockCounts(0, 0)))).ToList();
        return new InventoryPage(items, page, pageSize, refs.TotalCount, (refs.TotalCount + pageSize - 1) / pageSize);
    }

    public async Task<AdjustResult> AdjustAsync(
        Guid? actorId, Guid variantId, int delta, int expectedOnHand, string reason, CancellationToken ct)
    {
        VariantRef? variant = (await products.GetVariantsAsync([variantId], ct)).SingleOrDefault();
        if (variant is null)
        {
            return new AdjustResult(AdjustOutcome.NotFound, null, 0, 0);
        }

        return await unitOfWork.InTransactionAsync(async token =>
        {
            await store.EnsureRowAsync(variantId, token);
            InventoryStock row = (await store.LockAsync([variantId], token)).Single();
            if (row.QuantityOnHand != expectedOnHand)
            {
                return new AdjustResult(AdjustOutcome.Stale, null, row.QuantityOnHand, row.QuantityReserved);
            }

            DateTimeOffset now = clock.UtcNow;
            string before = JsonSerializer.Serialize(new StockState(row.QuantityOnHand, row.QuantityReserved), Json);
            try
            {
                row.Adjust(delta);
            }
            catch (DomainException)
            {
                string attempted = JsonSerializer.Serialize(new RejectedState(row.QuantityOnHand, row.QuantityReserved, delta), Json);
                audit.Add(new AuditEntry(actorId, "inventory.adjust.rejected", EntityType, variantId, before, attempted, reason, now));
                return new AdjustResult(AdjustOutcome.Insufficient, null, row.QuantityOnHand, row.QuantityReserved);
            }

            string after = JsonSerializer.Serialize(new StockState(row.QuantityOnHand, row.QuantityReserved), Json);
            audit.Add(new AuditEntry(actorId, "inventory.adjust", EntityType, variantId, before, after, reason, now));
            return new AdjustResult(
                AdjustOutcome.Ok, ToItem(variant, new StockCounts(row.QuantityOnHand, row.QuantityReserved)), row.QuantityOnHand, row.QuantityReserved);
        }, ct);
    }

    private InventoryItem ToItem(VariantRef v, StockCounts c) =>
        new(v.VariantId, v.ProductName, v.Sku, v.VariantName, v.IsActive, c.OnHand, c.Reserved, c.Available,
            StockLevel.Classify(c.Available, settings.LowStockThreshold));
}
