using CartAggregate = Ecommerce.Cart.Domain.Cart;

namespace Ecommerce.Cart.Application;

/// <summary>Cart limits, built once from <c>Cart:MaxQuantityPerLine</c> and <c>Store:Currency</c> (the module wires it up).</summary>
internal sealed record CartSettings(int MaxQuantityPerLine, string Currency);

/// <summary>
/// Cart persistence port. <see cref="EnsureAndLockAsync"/> needs an open transaction (it throws otherwise): it creates
/// the caller's cart if missing, then locks that row FOR UPDATE and loads its lines (lock order: carts, cart_items).
/// </summary>
internal interface ICartStore
{
    Task<CartAggregate> EnsureAndLockAsync(Guid userId, DateTimeOffset now, CancellationToken ct);

    /// <summary>No-tracking read. Null when the user has no cart yet (a read never creates one).</summary>
    Task<CartAggregate?> GetAsync(Guid userId, CancellationToken ct);
}
