using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Orders.Domain;

/// <summary>
/// An order. Checkout creates it with Place (Slice 4); the webhook and the expiry move it with MarkPaid and CancelForExpiry.
/// Totals and items are never touched after Place; every transition appends exactly one history row.
/// </summary>
internal sealed class Order
{
    public const string IllegalTransition = "illegal-transition";
    public const int MaxReasonLength = 500;
    public const string ReservationExpired = "reservation-expired";

    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusChange> _history = [];
    private readonly List<Shipment> _shipments = [];

    private Order() { }

    public Guid Id { get; private set; }
    public string OrderNumber { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal ShippingTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal GrandTotal { get; private set; }
    public string Currency { get; private set; } = "";
    public string ShippingMethodCode { get; private set; } = "";
    public string ShippingMethodName { get; private set; } = "";
    public ShippingAddress ShippingAddress { get; private set; } = null!;

    /// <summary>Optimistic concurrency token (D7): +1 on every transition.</summary>
    public int Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;
    public IReadOnlyList<OrderStatusChange> History => _history;
    public IReadOnlyList<Shipment> Shipments => _shipments;

    public static Order Place(
        Guid id, string orderNumber, Guid userId, string idempotencyKey, OrderTotals totals, string currency,
        string shippingMethodCode, string shippingMethodName, ShippingAddress address, IReadOnlyList<OrderItem> items, DateTimeOffset now)
    {
        Order order = new()
        {
            Id = id, OrderNumber = orderNumber, UserId = userId, IdempotencyKey = idempotencyKey, Status = OrderStatus.PendingPayment,
            Subtotal = totals.Subtotal, DiscountTotal = totals.DiscountTotal, ShippingTotal = totals.ShippingTotal,
            TaxTotal = totals.TaxTotal, GrandTotal = totals.GrandTotal, Currency = currency,
            ShippingMethodCode = shippingMethodCode, ShippingMethodName = shippingMethodName, ShippingAddress = address,
            Version = 0, CreatedAt = now, UpdatedAt = now,
        };
        order._items.AddRange(items);
        order._history.Add(OrderStatusChange.Create(id, null, OrderStatus.PendingPayment, userId, null, now));
        return order;
    }

    /// <summary>The verified webhook (D8). PendingPayment only; the actor is the system (null).</summary>
    public void MarkPaid(DateTimeOffset now) => Move(OrderStatus.Paid, null, null, now);

    /// <summary>The reservation ran out (arch 8.2 rule 7). PendingPayment only; the actor is the system (null).</summary>
    public void CancelForExpiry(DateTimeOffset now) => Move(OrderStatus.Cancelled, null, ReservationExpired, now);

    /// <summary>A refund reached the captured amount (arch 8.5): Paid or Processing to Refunded. The actor is the admin who asked (null: system).</summary>
    public void RefundInFull(Guid? actorId, DateTimeOffset now) => Move(OrderStatus.Refunded, actorId, "Refunded in full", now);

    /// <summary>A refund reached the captured amount on a Delivered order with an Approved return: Delivered to Returned.</summary>
    public void ReturnInFull(Guid? actorId, DateTimeOffset now) => Move(OrderStatus.Returned, actorId, "Return refunded", now);

    public void Process(Guid actorId, DateTimeOffset now) => Move(OrderStatus.Processing, actorId, null, now);

    public void Ship(Guid actorId, string carrierCode, string trackingNumber, DateTimeOffset now)
    {
        Move(OrderStatus.Shipped, actorId, null, now);
        _shipments.Add(Shipment.Create(Id, carrierCode, trackingNumber, now));
    }

    public void Deliver(Guid actorId, DateTimeOffset now)
    {
        Move(OrderStatus.Delivered, actorId, null, now);
        MarkShipmentsDelivered(now);
    }

    public void Override(Guid actorId, OrderStatus to, string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > MaxReasonLength)
        {
            throw new DomainException($"An override needs a reason of 1 to {MaxReasonLength} characters.");
        }
        if (!OrderStatusMachine.IsOverridable(Status, to))
        {
            throw new DomainException($"{IllegalTransition}: {Status} to {to} cannot be overridden.");
        }
        Move(to, actorId, reason, now);
        if (to == OrderStatus.Delivered)
        {
            MarkShipmentsDelivered(now);
        }
    }

    private void Require(OrderStatus to)
    {
        if (!OrderStatusMachine.IsLegal(Status, to))
        {
            throw new DomainException($"{IllegalTransition}: {Status} to {to}.");
        }
    }

    // Checks first, mutates after: a rejected transition leaves the aggregate untouched.
    private void Move(OrderStatus to, Guid? actorId, string? reason, DateTimeOffset now)
    {
        Require(to);
        OrderStatus from = Status;
        Status = to;
        Version++;
        UpdatedAt = now;
        _history.Add(OrderStatusChange.Create(Id, from, to, actorId, reason, now));
    }

    private void MarkShipmentsDelivered(DateTimeOffset now)
    {
        foreach (Shipment shipment in _shipments)
        {
            shipment.MarkDelivered(now);
        }
    }
}
