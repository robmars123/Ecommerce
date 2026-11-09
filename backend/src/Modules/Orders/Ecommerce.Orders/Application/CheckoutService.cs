using System.Security.Cryptography;
using System.Text;
using Ecommerce.Cart.Contracts;
using Ecommerce.Catalog.Contracts;
using Ecommerce.Inventory.Contracts;
using Ecommerce.Orders.Domain;
using Ecommerce.Payments.Contracts;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Orders.Application;

/// <summary>
/// Checkout (arch 8.2 rules 1 to 5, 10; Slice 4 D3, D4, D13). One short transaction: flush the idempotency key first (a
/// concurrent duplicate then waits on the key, not on the stock lock), re-read the cart and the prices, reserve stock in
/// variant-ID order, snapshot the order and its first payment attempt. The provider is called only after commit, so a
/// provider failure leaves a real PendingPayment order and the same key heals it.
/// </summary>
internal sealed class CheckoutService(
    IOrderStore orders,
    IOrderReadStore reads,
    ICartReader carts,
    IProductReader products,
    IInventoryReserver reserver,
    IPaymentAttempts payments,
    IIdempotencyStore keys,
    IUnitOfWork unitOfWork,
    IClock clock,
    ShippingMethods shipping,
    CheckoutSettings settings)
{
    public const string Scope = "checkout";
    public const string KeyConstraint = "pk_idempotency_keys";
    private const int MaxNameLength = 200;
    private static readonly TimeSpan KeyLifetime = TimeSpan.FromHours(24);

    /// <summary>A rejection decided inside the transaction. Thrown so the transaction rolls back, key row included.</summary>
    private sealed class Rejected(CheckoutResult result) : Exception("Checkout rejected.")
    {
        public CheckoutResult Result { get; } = result;
    }

    private abstract record Placement
    {
        private Placement() { }

        public sealed record Placed(Guid OrderId) : Placement;

        public sealed record Replayed(Guid OrderId) : Placement;

        public sealed record Conflict : Placement;
    }

    public async Task<CheckoutResult> CheckoutAsync(CheckoutCommand command, CancellationToken ct)
    {
        // Rule 10: an unknown method or a country we do not ship to is a 400 before any lock is taken.
        ShippingMethod? method = shipping.Find(command.ShippingMethodCode);
        if (method is null)
        {
            return new CheckoutResult.Invalid("shippingMethodCode", "Choose one of the available shipping methods.");
        }
        if (!shipping.ShipsTo(command.Address.Country))
        {
            return new CheckoutResult.Invalid("shippingAddress.country", "We do not ship to this country.");
        }

        string hash = HashOf(command);
        Placement placement;
        try
        {
            placement = await unitOfWork.InTransactionAsync(token => PlaceAsync(command, method, hash, token), ct);
        }
        catch (Rejected rejected)
        {
            return rejected.Result;
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == KeyConstraint)
        {
            // A concurrent request with the same key committed first. Its order is ours too (same user and body), or a conflict.
            placement = await ReplayAsync(command, hash, ct);
        }

        return placement switch
        {
            Placement.Placed p => await CompleteAsync(p.OrderId, ct),
            Placement.Replayed r => await CompleteAsync(r.OrderId, ct),
            _ => new CheckoutResult.KeyConflict(),
        };
    }

    private async Task<Placement> ReplayAsync(CheckoutCommand command, string hash, CancellationToken ct)
    {
        IdempotencyEntry? seen = await keys.FindAsync(Scope, command.IdempotencyKey, ct);
        return Matches(seen, command.UserId, hash, out Guid orderId) ? new Placement.Replayed(orderId) : new Placement.Conflict();
    }

    private async Task<Placement> PlaceAsync(CheckoutCommand command, ShippingMethod method, string hash, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        IdempotencyEntry? seen = await keys.FindAsync(Scope, command.IdempotencyKey, ct);
        if (seen is not null)
        {
            return Matches(seen, command.UserId, hash, out Guid existing) ? new Placement.Replayed(existing) : new Placement.Conflict();
        }

        Guid orderId = Guid.CreateVersion7(now);
        keys.Add(new IdempotencyEntry(Scope, command.IdempotencyKey, command.UserId, hash, orderId.ToString(), null, now, now + KeyLifetime));
        await unitOfWork.SaveChangesAsync(ct);

        IReadOnlyList<CartLine> cart = await carts.GetLinesAsync(command.UserId, ct);
        if (cart.Count == 0)
        {
            throw new Rejected(new CheckoutResult.CartEmpty());
        }

        List<Guid> variantIds = cart.Select(l => l.VariantId).ToList();
        Dictionary<Guid, VariantRef> refs = (await products.GetVariantsAsync(variantIds, ct)).ToDictionary(v => v.VariantId);
        Dictionary<Guid, VariantSnapshot> snapshots = (await products.GetVariantSnapshotsAsync(variantIds, ct)).ToDictionary(s => s.VariantId);

        List<RejectedLine> rejected = [];
        foreach (CartLine line in cart.OrderBy(l => l.VariantId))
        {
            if (!refs.TryGetValue(line.VariantId, out VariantRef? variant) || !variant.IsActive
                || !snapshots.TryGetValue(line.VariantId, out VariantSnapshot? snapshot) || !snapshot.IsActive)
            {
                rejected.Add(new RejectedLine(line.VariantId, LineReasons.Unavailable));
            }
            else if (snapshot.Currency != settings.Currency)
            {
                throw new Rejected(new CheckoutResult.Invalid("cart", "A cart item is not priced in the store currency."));
            }
            else if (snapshot.Price != line.DisplayUnitPrice)
            {
                rejected.Add(new RejectedLine(line.VariantId, LineReasons.PriceChanged));
            }
        }
        if (rejected.Count > 0)
        {
            throw new Rejected(new CheckoutResult.LinesRejected(rejected));
        }

        DateTimeOffset expiresAt = now.AddMinutes(settings.ReservationTtlMinutes);
        ReservationResult reservation = await reserver.ReserveAsync(
            orderId, cart.Select(l => new ReservationLine(l.VariantId, l.Quantity)).ToList(), expiresAt, ct);
        if (!reservation.Succeeded)
        {
            throw new Rejected(new CheckoutResult.LinesRejected(
                reservation.Shortfalls.OrderBy(s => s.VariantId).Select(s => new RejectedLine(s.VariantId, LineReasons.OutOfStock, s.Available)).ToList()));
        }

        // Prices come from Catalog just now, never from the cart or the client (rule 1).
        List<OrderItem> items = cart.OrderBy(l => l.VariantId).Select(l =>
        {
            VariantRef variant = refs[l.VariantId];
            string name = $"{variant.ProductName} - {variant.VariantName}";
            return OrderItem.Create(
                Guid.CreateVersion7(now), orderId, l.VariantId, variant.Sku,
                name.Length <= MaxNameLength ? name : name[..MaxNameLength], snapshots[l.VariantId].Price, l.Quantity);
        }).ToList();
        OrderTotals totals = OrderTotals.Compute(items.Select(i => i.LineTotal), method.Amount, settings.TaxRatePercent);

        long number = await orders.NextOrderNumberAsync(ct);
        Order order = Order.Place(
            orderId, $"ORD-{now.Year}-{number:D6}", command.UserId, command.IdempotencyKey, totals, settings.Currency,
            method.Code, method.Name, command.Address, items, now);
        orders.Add(order);
        await payments.StartAttemptAsync(Guid.CreateVersion7(now), orderId, order.GrandTotal, order.Currency, ct);
        return new Placement.Placed(orderId);
    }

    /// <summary>After commit, no transaction: read the order back and make sure a PendingPayment order has its provider intent (rule 4).</summary>
    private async Task<CheckoutResult> CompleteAsync(Guid orderId, CancellationToken ct)
    {
        OrderDetailRow order = await reads.GetAsync(orderId, ct) ?? throw new InvalidOperationException($"Order {orderId} was committed but cannot be read.");
        if (order.Status != OrderStatus.PendingPayment)
        {
            return new CheckoutResult.Ok(order, null);
        }

        PaymentAttemptInfo latest = await payments.GetLatestAsync(orderId, ct) ?? throw new InvalidOperationException($"Order {orderId} has no payment attempt.");
        try
        {
            PaymentIntentHandle handle = await payments.EnsureIntentAsync(latest.PaymentId, ct);
            return new CheckoutResult.Ok(order, handle.ClientSecret);
        }
        catch (PaymentProviderUnavailableException)
        {
            return new CheckoutResult.ProviderUnavailable(orderId);
        }
    }

    private static bool Matches(IdempotencyEntry? seen, Guid userId, string hash, out Guid orderId)
    {
        orderId = Guid.Empty;
        return seen is not null && seen.UserId == userId && seen.RequestHash == hash && Guid.TryParse(seen.ResultRef, out orderId);
    }

    /// <summary>SHA-256 over the trimmed body, in a fixed field order; each field is length-prefixed so a newline inside one cannot shift a boundary.</summary>
    private static string HashOf(CheckoutCommand c)
    {
        ShippingAddress a = c.Address;
        string text = string.Concat(new[] { c.ShippingMethodCode, a.FullName, a.Line1, a.Line2 ?? "", a.City, a.Region ?? "", a.PostalCode, a.Country }.Select(f => $"{f.Length}:{f}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
