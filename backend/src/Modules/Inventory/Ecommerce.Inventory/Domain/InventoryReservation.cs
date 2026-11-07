using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Inventory.Domain;

internal sealed class InventoryReservation
{
    private InventoryReservation() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductVariantId { get; private set; }
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static InventoryReservation Create(Guid id, Guid orderId, Guid variantId, int quantity, DateTimeOffset expiresAt, DateTimeOffset now) =>
        new()
        {
            Id = id, OrderId = orderId, ProductVariantId = variantId, Quantity = quantity,
            Status = ReservationStatus.Active, ExpiresAt = expiresAt, CreatedAt = now,
        };

    public void Commit()
    {
        if (Status != ReservationStatus.Active)
        {
            throw new DomainException("Only an active reservation can be committed.");
        }
        Status = ReservationStatus.Committed;
    }

    public void Expire()
    {
        if (Status != ReservationStatus.Active)
        {
            throw new DomainException("Only an active reservation can expire.");
        }
        Status = ReservationStatus.Expired;
    }
}
