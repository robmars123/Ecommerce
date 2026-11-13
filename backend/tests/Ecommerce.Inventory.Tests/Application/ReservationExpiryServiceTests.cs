using Ecommerce.Inventory.Contracts;
using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Domain;
using Ecommerce.Orders.Contracts;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Inventory.Tests.Application;

public class ReservationExpiryServiceTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    /// <summary>No order exists for these reservations (Slice 2 fixtures): Orders reports nothing to cancel, so the Slice 2 behaviour is unchanged.</summary>
    private sealed class NoOrderExpiry : IOrderExpiry
    {
        public Task<bool> CancelExpiredAsync(Guid orderId, CancellationToken ct) => Task.FromResult(false);
    }

    /// <summary>Reports an order as expired in the unlocked search, then hands back whatever the locked read is told to.</summary>
    private sealed class FakeStore(Guid orderId, InventoryReservation reservation, InventoryStock stock) : IInventoryStore
    {
    public Task<IReadOnlyList<LowStockItem>> ListLowAsync(int threshold, int max, CancellationToken ct) => throw new NotSupportedException();
    public Task<int> CountLowAsync(int threshold, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> FindExpiredOrderIdsAsync(DateTimeOffset now, int max, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>([orderId]);

        public Task<IReadOnlyList<InventoryReservation>> LockActiveReservationsAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<InventoryReservation>>([reservation]);

        public Task<IReadOnlyList<InventoryStock>> LockAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<InventoryStock>>([stock]);

        public Task EnsureRowAsync(Guid variantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, StockCounts>> GetAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct) => throw new NotSupportedException();
        public void AddReservation(InventoryReservation r) => throw new NotSupportedException();
    }

    private static (ReservationExpiryService Service, InventoryReservation Reservation, InventoryStock Stock) Build(DateTimeOffset now, DateTimeOffset expiresAt)
    {
        Guid variant = Guid.NewGuid();
        Guid order = Guid.NewGuid();
        InventoryStock stock = InventoryStock.Create(variant);
        stock.Adjust(5);
        stock.Reserve(2);
        InventoryReservation reservation = InventoryReservation.Create(Guid.NewGuid(), order, variant, 2, expiresAt, now.AddMinutes(-20));
        return (new ReservationExpiryService(new FakeStore(order, reservation, stock), new PassThroughUnitOfWork(), new FixedClock(now), new NoOrderExpiry()), reservation, stock);
    }

    [Fact]
    public async Task A_reservation_whose_deadline_moved_after_the_search_is_left_alone_under_the_lock()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        (ReservationExpiryService service, InventoryReservation reservation, InventoryStock stock) = Build(now, now.AddMinutes(5));

        int expired = await service.SweepAsync(CancellationToken.None);

        Assert.Equal(0, expired);
        Assert.Equal(ReservationStatus.Active, reservation.Status);
        Assert.Equal(2, stock.QuantityReserved);
    }

    [Fact]
    public async Task A_due_reservation_is_expired_and_its_quantity_released()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        (ReservationExpiryService service, InventoryReservation reservation, InventoryStock stock) = Build(now, now.AddMinutes(-1));

        int expired = await service.SweepAsync(CancellationToken.None);

        Assert.Equal(1, expired);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Equal(0, stock.QuantityReserved);
    }
}
