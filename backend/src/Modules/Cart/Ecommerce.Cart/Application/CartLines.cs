using Ecommerce.Cart.Contracts;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Cart.Application;

/// <summary>Cart lines for Orders (Slice 4 D2): checkout reads them, the Paid transition removes them. Both contracts, one class.</summary>
internal sealed class CartLines(ICartStore store, IClock clock) : ICartReader, ICartWriter
{
    public async Task<IReadOnlyList<CartLine>> GetLinesAsync(Guid userId, CancellationToken ct)
    {
        Domain.Cart? cart = await store.GetAsync(userId, ct);
        return cart is null
            ? []
            : cart.Items.Select(i => new CartLine(i.ProductVariantId, i.Quantity, i.DisplayUnitPrice, i.Currency)).ToList();
    }

    public async Task RemoveLinesAsync(Guid userId, IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        // Locks the carts row (last in the lock order, D3); a user with no cart gets an empty one, which is harmless.
        Domain.Cart cart = await store.EnsureAndLockAsync(userId, now, ct);
        foreach (Guid variantId in variantIds)
        {
            cart.Remove(variantId, now);
        }
    }
}
