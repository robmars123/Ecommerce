namespace Ecommerce.Cart.Contracts;

public sealed record CartLine(Guid VariantId, int Quantity, decimal DisplayUnitPrice, string Currency);

/// <summary>The caller's cart lines as stored (no re-pricing). Empty when the user has no cart.</summary>
public interface ICartReader
{
    Task<IReadOnlyList<CartLine>> GetLinesAsync(Guid userId, CancellationToken ct);
}

/// <summary>Removes lines once their order is Paid. Locks the cart row; runs inside the caller's transaction.</summary>
public interface ICartWriter
{
    Task RemoveLinesAsync(Guid userId, IReadOnlyCollection<Guid> variantIds, CancellationToken ct);
}
