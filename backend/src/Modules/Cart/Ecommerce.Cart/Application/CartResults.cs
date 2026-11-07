using Ecommerce.Catalog.Contracts;
using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Cart.Application;

internal enum CartIssue
{
    PriceChanged,
    ExceedsStock,
    Unavailable,
}

internal sealed record CartLineView(
    Guid VariantId, string ProductSlug, string ProductName, string VariantName, CartImage? Image,
    int Quantity, decimal UnitPrice, string Currency, decimal LineTotal, StockStatus StockStatus,
    IReadOnlyList<CartIssue> Issues);

/// <summary>Display totals only (checkout recomputes). Subtotal sums lines without the Unavailable flag, in the store currency.</summary>
internal sealed record CartView(IReadOnlyList<CartLineView> Items, decimal Subtotal, string Currency);

internal enum CartOutcome
{
    Ok,
    NotFound,
    Invalid,
    Insufficient,
}

/// <summary>Cart is set for Ok. Available is set for Insufficient. Field and Message describe an Invalid request.</summary>
internal sealed record CartWriteResult(CartOutcome Outcome, CartView? Cart = null, int Available = 0, string? Field = null, string? Message = null);

internal enum MergeOutcome
{
    Kept,
    Capped,
    Removed,
}

internal enum MergeReason
{
    Unavailable,
    OutOfStock,
    LimitReached,
}

internal sealed record MergeLine(Guid VariantId, int Quantity);

internal sealed record MergeLineResult(Guid VariantId, MergeOutcome Outcome, int Quantity, MergeReason? Reason);

internal sealed record MergeResult(CartView Cart, IReadOnlyList<MergeLineResult> Results);

internal enum MergeStatus
{
    Ok,
    Conflict,
}

internal sealed record MergeReply(MergeStatus Status, MergeResult? Result = null);
