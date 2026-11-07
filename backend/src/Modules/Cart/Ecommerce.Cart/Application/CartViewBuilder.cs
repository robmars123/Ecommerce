using Ecommerce.Cart.Domain;
using Ecommerce.Catalog.Contracts;
using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Cart.Application;

/// <summary>
/// Builds the cart the customer sees (arch 8.1, D5): every line is re-priced from Catalog and flagged. It reads
/// display-grade stock with no lock. The stored display price is only compared, never used for a total.
/// </summary>
internal sealed class CartViewBuilder(IProductReader products, IInventoryReader inventory, CartSettings settings)
{
    public async Task<CartView> BuildAsync(IReadOnlyCollection<CartItem> items, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return new CartView([], 0m, settings.Currency);
        }

        List<Guid> ids = items.Select(i => i.ProductVariantId).ToList();
        Dictionary<Guid, VariantSnapshot> snapshots = (await products.GetVariantSnapshotsAsync(ids, ct)).ToDictionary(s => s.VariantId);
        IReadOnlyDictionary<Guid, StockAvailability> stock = await inventory.GetAvailabilityAsync(ids, ct);

        List<CartLineView> lines = [];
        decimal subtotal = 0m;
        foreach (CartItem item in items.OrderBy(i => i.AddedAt).ThenBy(i => i.Id))
        {
            snapshots.TryGetValue(item.ProductVariantId, out VariantSnapshot? snapshot);
            StockAvailability availability = stock.GetValueOrDefault(item.ProductVariantId, new StockAvailability(0, StockStatus.Out));
            bool unavailable = snapshot is null || !snapshot.IsActive || snapshot.Currency != settings.Currency;

            decimal price = snapshot?.Price ?? item.DisplayUnitPrice;
            string currency = snapshot?.Currency ?? item.Currency;
            List<CartIssue> issues = [];
            if (unavailable)
            {
                issues.Add(CartIssue.Unavailable);
            }
            else
            {
                if (price != item.DisplayUnitPrice)
                {
                    issues.Add(CartIssue.PriceChanged);
                }
                if (item.Quantity > availability.Available)
                {
                    issues.Add(CartIssue.ExceedsStock);
                }
                subtotal += price * item.Quantity;
            }

            lines.Add(new CartLineView(
                item.ProductVariantId, snapshot?.ProductSlug ?? "", snapshot?.ProductName ?? "", snapshot?.VariantName ?? "", snapshot?.Image,
                item.Quantity, price, currency, price * item.Quantity, availability.Status, issues));
        }
        return new CartView(lines, subtotal, settings.Currency);
    }
}
