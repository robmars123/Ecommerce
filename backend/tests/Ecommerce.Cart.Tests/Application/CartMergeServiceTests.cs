using Ecommerce.Cart.Application;
using Ecommerce.Catalog.Contracts;
using Ecommerce.Inventory.Contracts;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;
using CartAggregate = Ecommerce.Cart.Domain.Cart;

namespace Ecommerce.Cart.Tests.Application;

// Test 22 (unit half): every branch of the D6 outcome table, with fakes.
public class CartMergeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.NewGuid();

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    private sealed class FakeCartStore(CartAggregate cart) : ICartStore
    {
        public Task<CartAggregate> EnsureAndLockAsync(Guid userId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(cart);
        public Task<CartAggregate?> GetAsync(Guid userId, CancellationToken ct) => Task.FromResult<CartAggregate?>(cart);
    }

    private sealed class FakeProducts(Dictionary<Guid, VariantSnapshot> snapshots) : IProductReader
    {
        public Task<IReadOnlyList<VariantSnapshot>> GetVariantSnapshotsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<VariantSnapshot>>(snapshots.Where(kv => variantIds.Contains(kv.Key)).Select(kv => kv.Value).ToList());

        public Task<IReadOnlyList<VariantRef>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<VariantRefPage> ListVariantsAsync(int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeInventory(Dictionary<Guid, int> available) : IInventoryReader
    {
        public Task<IReadOnlyList<LowStockItem>> ListLowStockAsync(int max, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, StockAvailability>> GetAvailabilityAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, StockAvailability>>(variantIds.Distinct().ToDictionary(
                id => id, id => new StockAvailability(available.GetValueOrDefault(id), available.GetValueOrDefault(id) > 0 ? StockStatus.InStock : StockStatus.Out)));

        public Task<IReadOnlyDictionary<Guid, StockStatus>> GetStockStatusesAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeKeys : IIdempotencyStore
    {
        public List<IdempotencyEntry> Stored { get; } = [];
        public Task<IdempotencyEntry?> FindAsync(string scope, string key, CancellationToken ct) =>
            Task.FromResult(Stored.FirstOrDefault(e => e.Scope == scope && e.Key == key));
        public void Add(IdempotencyEntry entry) => Stored.Add(entry);
    }

    private static VariantSnapshot Snapshot(Guid id, bool active = true, string currency = "USD") =>
        new(id, "slug", "Product", "Variant", null, 10m, currency, active);

    private static (CartMergeService Service, CartAggregate Cart, FakeKeys Keys) Build(
        Guid variant, int serverQuantity, int available, bool active = true, string currency = "USD", bool known = true)
    {
        CartAggregate cart = CartAggregate.Create(Guid.NewGuid(), User, Now);
        if (serverQuantity > 0)
        {
            cart.Set(variant, serverQuantity, 10m, "USD", Now);
        }
        CartSettings settings = new(10, "USD");
        FakeProducts products = new(known ? new Dictionary<Guid, VariantSnapshot> { [variant] = Snapshot(variant, active, currency) } : []);
        FakeInventory inventory = new(new Dictionary<Guid, int> { [variant] = available });
        FakeKeys keys = new();
        CartViewBuilder views = new(products, inventory, settings);
        return (new CartMergeService(new FakeCartStore(cart), products, inventory, views, keys, new PassThroughUnitOfWork(), new FixedClock(), settings), cart, keys);
    }

    private static async Task<MergeLineResult> RunAsync(CartMergeService service, Guid variant, int guest, string key = "k1")
    {
        MergeReply reply = await service.MergeAsync(User, key, [new MergeLine(variant, guest)], CancellationToken.None);
        Assert.Equal(MergeStatus.Ok, reply.Status);
        return Assert.Single(reply.Result!.Results);
    }

    [Theory]
    [InlineData(2, 3, 20, "Kept", 5, null)]
    [InlineData(0, 3, 20, "Kept", 3, null)]
    [InlineData(8, 5, 50, "Capped", 10, "LimitReached")]
    [InlineData(1, 6, 4, "Capped", 4, "OutOfStock")]
    [InlineData(5, 2, 3, "Removed", 5, "OutOfStock")]
    [InlineData(5, 2, 5, "Removed", 5, "OutOfStock")]
    [InlineData(10, 3, 50, "Removed", 10, "LimitReached")]
    [InlineData(0, 2, 0, "Removed", 0, "OutOfStock")]
    [InlineData(0, 4, 10, "Kept", 4, null)]
    [InlineData(0, 12, 50, "Capped", 10, "LimitReached")]
    public async Task The_outcome_table(int server, int guest, int available, string outcome, int quantity, string? reason)
    {
        Guid v = Guid.NewGuid();
        (CartMergeService service, CartAggregate cart, FakeKeys _) = Build(v, server, available);

        MergeLineResult result = await RunAsync(service, v, guest);

        Assert.Equal((outcome, quantity, reason), (result.Outcome.ToString(), result.Quantity, result.Reason?.ToString()));
        Assert.Equal(quantity, cart.QuantityOf(v));
    }

    [Theory]
    [InlineData(false, "USD", true)]
    [InlineData(true, "EUR", true)]
    [InlineData(true, "USD", false)]
    public async Task An_inactive_other_currency_or_unknown_variant_is_Removed_Unavailable_and_the_cart_is_unchanged(bool active, string currency, bool known)
    {
        Guid v = Guid.NewGuid();
        (CartMergeService service, CartAggregate cart, FakeKeys _) = Build(v, 0, 20, active, currency, known);

        MergeLineResult result = await RunAsync(service, v, 2);

        Assert.Equal((MergeOutcome.Removed, 0, MergeReason.Unavailable), (result.Outcome, result.Quantity, result.Reason));
        Assert.Empty(cart.Items);
    }

    [Fact]
    public async Task A_repeated_key_with_the_same_body_replays_the_stored_result_and_a_changed_body_or_user_conflicts()
    {
        Guid v = Guid.NewGuid();
        (CartMergeService service, CartAggregate cart, FakeKeys keys) = Build(v, 2, 20);

        MergeLineResult first = await RunAsync(service, v, 3);
        MergeLineResult replay = await RunAsync(service, v, 3);

        Assert.Equal(first, replay);
        Assert.Equal(5, cart.QuantityOf(v));
        Assert.Single(keys.Stored);
        Assert.Equal(MergeStatus.Conflict, (await service.MergeAsync(User, "k1", [new MergeLine(v, 4)], CancellationToken.None)).Status);
        Assert.Equal(MergeStatus.Conflict, (await service.MergeAsync(Guid.NewGuid(), "k1", [new MergeLine(v, 3)], CancellationToken.None)).Status);
    }
}
