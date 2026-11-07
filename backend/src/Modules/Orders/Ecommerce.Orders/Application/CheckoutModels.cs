using Ecommerce.Orders.Domain;

namespace Ecommerce.Orders.Application;

/// <summary>What the client sent, trimmed and validated. No price, tax, total or status exists here (rule 1).</summary>
internal sealed record CheckoutCommand(Guid UserId, string IdempotencyKey, ShippingAddress Address, string ShippingMethodCode);

internal static class LineReasons
{
    public const string OutOfStock = "OutOfStock";
    public const string PriceChanged = "PriceChanged";
    public const string Unavailable = "Unavailable";
}

internal sealed record RejectedLine(Guid VariantId, string Reason, int? Available = null);

/// <summary>Ok | KeyConflict | CartEmpty | LinesRejected | Invalid | ProviderUnavailable. The order is in Ok and ProviderUnavailable (the latter repeats with the same key).</summary>
internal abstract record CheckoutResult
{
    private CheckoutResult() { }

    /// <summary>ClientSecret is null unless the order is still PendingPayment.</summary>
    public sealed record Ok(OrderDetailRow Order, string? ClientSecret) : CheckoutResult;

    public sealed record KeyConflict : CheckoutResult;

    public sealed record CartEmpty : CheckoutResult;

    public sealed record LinesRejected(IReadOnlyList<RejectedLine> Lines) : CheckoutResult;

    public sealed record Invalid(string Field, string Message) : CheckoutResult;

    public sealed record ProviderUnavailable(Guid OrderId) : CheckoutResult;
}

/// <summary>Ok | NotFound | Forbidden | NotPending | ReservationExpired | KeyConflict | ProviderUnavailable.</summary>
internal abstract record RetryResult
{
    private RetryResult() { }

    public sealed record Ok(Guid OrderId, int Attempt, string ClientSecret) : RetryResult;

    public sealed record NotFound : RetryResult;

    public sealed record Forbidden : RetryResult;

    public sealed record NotPending : RetryResult;

    public sealed record ReservationExpired : RetryResult;

    public sealed record KeyConflict : RetryResult;

    public sealed record ProviderUnavailable(Guid OrderId) : RetryResult;
}
