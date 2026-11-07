using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Domain;
using Ecommerce.Orders.Contracts;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Inventory.Tests.Application;

/// <summary>Test 4 (unit half, Slice 4 D11): Orders cancels first; one failing order never stops the sweep; a cancel with nothing due is undone.</summary>
public class ReservationExpirySweepTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    private sealed class Order(Guid id, InventoryReservation reservation, InventoryStock stock)
    {
        public Guid Id { get; } = id;
        public InventoryReservation Reservation { get; } = reservation;
        public InventoryStock Stock { get; } = stock;
    }

    private sealed class FakeStore(IReadOnlyList<Order> orders) : IInventoryStore
    {
        public Task<IReadOnlyList<Guid>> FindExpiredOrderIdsAsync(DateTimeOffset now, int max, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>(orders.Select(o => o.Id).ToList());

        public Task<IReadOnlyList<InventoryReservation>> LockActiveReservationsAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<InventoryReservation>>([orders.Single(o => o.Id == id).Reservation]);

        public Task<IReadOnlyList<InventoryStock>> LockAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<InventoryStock>>(orders.Where(o => variantIds.Contains(o.Stock.ProductVariantId)).Select(o => o.Stock).ToList());

        public Task EnsureRowAsync(Guid variantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, StockCounts>> GetAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct) => throw new NotSupportedException();
        public void AddReservation(InventoryReservation r) => throw new NotSupportedException();
    }

    private sealed class ScriptedOrderExpiry(Func<Guid, bool> cancel) : IOrderExpiry
    {
        public List<Guid> Asked { get; } = [];

        public Task<bool> CancelExpiredAsync(Guid orderId, CancellationToken ct)
        {
            Asked.Add(orderId);
            return Task.FromResult(cancel(orderId));
        }
    }

    private static Order Due(DateTimeOffset expiresAt)
    {
        Guid variant = Guid.NewGuid();
        InventoryStock stock = InventoryStock.Create(variant);
        stock.Adjust(5);
        stock.Reserve(2);
        Guid orderId = Guid.NewGuid();
        return new Order(orderId, InventoryReservation.Create(Guid.NewGuid(), orderId, variant, 2, expiresAt, Now.AddMinutes(-20)), stock);
    }

    private static ReservationExpiryService Service(IReadOnlyList<Order> orders, IOrderExpiry expiry) =>
        new(new FakeStore(orders), new PassThroughUnitOfWork(), new FixedClock(Now), expiry);

    [Fact]
    public async Task Orders_is_asked_to_cancel_each_order_before_its_reservations_are_expired()
    {
        Order order = Due(Now.AddMinutes(-1));
        ScriptedOrderExpiry expiry = new(_ => true);

        int expired = await Service([order], expiry).SweepAsync(CancellationToken.None);

        Assert.Equal(1, expired);
        Assert.Equal([order.Id], expiry.Asked);
        Assert.Equal(ReservationStatus.Expired, order.Reservation.Status);
        Assert.Equal(0, order.Stock.QuantityReserved);
    }

    [Fact]
    public async Task One_failing_order_does_not_stop_the_sweep_and_the_failure_is_reported_afterwards()
    {
        Order failing = Due(Now.AddMinutes(-1));
        Order healthy = Due(Now.AddMinutes(-1));
        ScriptedOrderExpiry expiry = new(id => id == failing.Id ? throw new InvalidOperationException("boom") : false);

        AggregateException error = await Assert.ThrowsAsync<AggregateException>(() => Service([failing, healthy], expiry).SweepAsync(CancellationToken.None));

        Assert.Single(error.InnerExceptions);
        Assert.Equal(ReservationStatus.Expired, healthy.Reservation.Status);
        Assert.Equal(0, healthy.Stock.QuantityReserved);
        Assert.Equal(ReservationStatus.Active, failing.Reservation.Status);
    }

    [Fact]
    public async Task A_cancelled_order_with_nothing_due_fails_so_the_cancel_is_rolled_back()
    {
        Order order = Due(Now.AddMinutes(5));
        ScriptedOrderExpiry expiry = new(_ => true);

        AggregateException error = await Assert.ThrowsAsync<AggregateException>(() => Service([order], expiry).SweepAsync(CancellationToken.None));

        Assert.Contains("none of its reservations is due", error.InnerExceptions.Single().InnerException!.Message);
        Assert.Equal(ReservationStatus.Active, order.Reservation.Status);
    }
}
