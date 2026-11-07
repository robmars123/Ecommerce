using Ecommerce.Catalog.Contracts;
using Ecommerce.Inventory.Contracts;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;
using CartAggregate = Ecommerce.Cart.Domain.Cart;

namespace Ecommerce.Cart.Application;

/// <summary>
/// Server cart reads and writes (arch 8.1, D2 to D5). Every write runs in one transaction that locks the caller's cart
/// row first, so two writes to one cart never lose an update. The request never carries a price: it is read from Catalog.
/// A rejection changes no line.
/// </summary>
internal sealed class CartService(
    ICartStore store,
    IProductReader products,
    IInventoryReader inventory,
    CartViewBuilder views,
    IUnitOfWork unitOfWork,
    IClock clock,
    CartSettings settings)
{
    public async Task<CartView> GetAsync(Guid userId, CancellationToken ct)
    {
        CartAggregate? cart = await store.GetAsync(userId, ct);
        return await views.BuildAsync(cart?.Items ?? [], ct);
    }

    /// <summary>Adds the quantity to the line (creating it).</summary>
    public Task<CartWriteResult> AddAsync(Guid userId, Guid variantId, int quantity, CancellationToken ct) =>
        WriteAsync(userId, variantId, quantity, add: true, ct);

    /// <summary>Sets the line to the quantity. 0 removes it. A line not in the cart is NotFound.</summary>
    public Task<CartWriteResult> SetAsync(Guid userId, Guid variantId, int quantity, CancellationToken ct) =>
        WriteAsync(userId, variantId, quantity, add: false, ct);

    public Task<CartWriteResult> RemoveAsync(Guid userId, Guid variantId, CancellationToken ct) =>
        unitOfWork.InTransactionAsync(async token =>
        {
            CartAggregate cart = await store.EnsureAndLockAsync(userId, clock.UtcNow, token);
            cart.Remove(variantId, clock.UtcNow);
            return new CartWriteResult(CartOutcome.Ok);
        }, ct);

    private Task<CartWriteResult> WriteAsync(Guid userId, Guid variantId, int quantity, bool add, CancellationToken ct) =>
        unitOfWork.InTransactionAsync(async token =>
        {
            DateTimeOffset now = clock.UtcNow;
            CartAggregate cart = await store.EnsureAndLockAsync(userId, now, token);
            int existing = cart.QuantityOf(variantId);
            if (!add && existing == 0)
            {
                return new CartWriteResult(CartOutcome.NotFound);
            }

            if (!add && quantity == 0)
            {
                cart.Remove(variantId, now);
                return new CartWriteResult(CartOutcome.Ok, await views.BuildAsync(cart.Items, token));
            }

            VariantSnapshot? snapshot = (await products.GetVariantSnapshotsAsync([variantId], token)).SingleOrDefault();
            if (snapshot is null || !snapshot.IsActive || snapshot.Currency != settings.Currency)
            {
                return Invalid("variantId", "This item is not available.");
            }

            int resulting = add ? existing + quantity : quantity;
            if (resulting < 1 || resulting > settings.MaxQuantityPerLine)
            {
                return Invalid("quantity", $"A line can hold 1 to {settings.MaxQuantityPerLine} of an item.");
            }

            int available = (await inventory.GetAvailabilityAsync([variantId], token))[variantId].Available;
            if (resulting > available)
            {
                return new CartWriteResult(CartOutcome.Insufficient, Available: available);
            }

            cart.Set(variantId, resulting, snapshot.Price, snapshot.Currency, now);
            return new CartWriteResult(CartOutcome.Ok, await views.BuildAsync(cart.Items, token));
        }, ct);

    private static CartWriteResult Invalid(string field, string message) =>
        new(CartOutcome.Invalid, Field: field, Message: message);
}
