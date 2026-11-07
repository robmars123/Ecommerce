using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Contracts;
using Ecommerce.Inventory.Domain;
using Ecommerce.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests.Inventory;

// Test 1 (reserver half; Slice 4 repeats it through POST /checkout).
public class ReserveConcurrencyTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    private static async Task<ReservationResult[]> RunTogetherAsync(IReadOnlyList<Func<Task<ReservationResult>>> work)
    {
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<Task<ReservationResult>> tasks = work.Select(w => Task.Run(async () =>
        {
            await gate.Task;
            return await w();
        })).ToList();
        gate.SetResult();
        return await Task.WhenAll(tasks).WaitAsync(Limit);
    }

    [Fact]
    public async Task T1_one_unit_ten_parallel_reservations_exactly_one_succeeds_and_nine_are_rejected_without_an_exception()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid variant = await InventoryEnv.AddVariantAsync(env, "ONE-1");
        await InventoryEnv.SetStockAsync(env, variant, 1);

        ReservationResult[] results = await RunTogetherAsync(Enumerable.Range(0, 10)
            .Select<int, Func<Task<ReservationResult>>>(_ => () => InventoryEnv.ReserveAsync(env, Guid.NewGuid(), [new ReservationLine(variant, 1)]))
            .ToList());

        Assert.Equal(1, results.Count(r => r.Succeeded));
        ReservationResult[] rejected = results.Where(r => !r.Succeeded).ToArray();
        Assert.Equal(9, rejected.Length);
        Assert.All(rejected, r => Assert.Equal(new ReservationShortfall(variant, 1, 0), Assert.Single(r.Shortfalls)));
        Assert.Equal(1, await InventoryEnv.CountAsync(env, "SELECT count(*) FROM inventory_reservations WHERE product_variant_id = @id AND status = 'Active'", ("id", variant)));
        Assert.Equal((1, 1), await InventoryEnv.ReadAsync(env, variant));
    }

    [Fact]
    public async Task T1_twenty_parallel_reservations_over_two_variants_in_opposite_line_order_finish_without_deadlock_and_never_oversell()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid a = await InventoryEnv.AddVariantAsync(env, "AAA-1");
        Guid b = await InventoryEnv.AddVariantAsync(env, "BBB-1");
        await InventoryEnv.SetStockAsync(env, a, 2);
        await InventoryEnv.SetStockAsync(env, b, 2);

        ReservationResult[] results = await RunTogetherAsync(Enumerable.Range(0, 20)
            .Select<int, Func<Task<ReservationResult>>>(i => () => InventoryEnv.ReserveAsync(
                env, Guid.NewGuid(), i % 2 == 0 ? [new ReservationLine(a, 1), new ReservationLine(b, 1)] : [new ReservationLine(b, 1), new ReservationLine(a, 1)]))
            .ToList());

        Assert.Equal(2, results.Count(r => r.Succeeded));
        Assert.Equal((2, 2), await InventoryEnv.ReadAsync(env, a));
        Assert.Equal((2, 2), await InventoryEnv.ReadAsync(env, b));
        Assert.Equal(4, await InventoryEnv.CountAsync(env, "SELECT count(*) FROM inventory_reservations WHERE status = 'Active'"));
    }

    [Fact]
    public async Task Stock_rows_are_locked_in_database_uuid_order_whatever_order_they_were_inserted_or_asked_for()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        // uuid text order (lower-case hex) is the order Postgres sorts and locks in; .NET Guid.CompareTo differs.
        List<Guid> ascending = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).OrderBy(g => g.ToString("D"), StringComparer.Ordinal).ToList();
        foreach (Guid id in Enumerable.Reverse(ascending))
        {
            await InventoryEnv.SetStockAsync(env, id, 1);
        }
        using IServiceScope scope = env.Factory.Services.CreateScope();
        IInventoryStore store = scope.ServiceProvider.GetRequiredService<IInventoryStore>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        IReadOnlyList<InventoryStock> locked = await unitOfWork.InTransactionAsync(
            ct => store.LockAsync(Enumerable.Reverse(ascending).ToList(), ct), TestContext.Current.CancellationToken);

        Assert.Equal(ascending, locked.Select(s => s.ProductVariantId).ToList());
    }

    [Fact]
    public async Task A_partly_short_order_reserves_nothing_and_lists_every_short_line()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid ok = await InventoryEnv.AddVariantAsync(env, "OK-1");
        Guid low = await InventoryEnv.AddVariantAsync(env, "LOW-1");
        Guid none = Guid.NewGuid();
        await InventoryEnv.SetStockAsync(env, ok, 5);
        await InventoryEnv.SetStockAsync(env, low, 1);

        ReservationResult result = await InventoryEnv.ReserveAsync(
            env, Guid.NewGuid(), [new ReservationLine(ok, 2), new ReservationLine(low, 3), new ReservationLine(none, 1)]);

        Assert.False(result.Succeeded);
        Assert.Equivalent(new[] { new ReservationShortfall(low, 3, 1), new ReservationShortfall(none, 1, 0) }, result.Shortfalls);
        Assert.Equal((5, 0), await InventoryEnv.ReadAsync(env, ok));
        Assert.Equal((1, 0), await InventoryEnv.ReadAsync(env, low));
        Assert.Equal(0, await InventoryEnv.CountAsync(env, "SELECT count(*) FROM inventory_reservations"));
    }

    [Fact]
    public async Task Reserving_outside_a_transaction_throws()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid variant = await InventoryEnv.AddVariantAsync(env, "TX-1");
        await InventoryEnv.SetStockAsync(env, variant, 3);
        using IServiceScope scope = env.Factory.Services.CreateScope();
        IInventoryReserver reserver = scope.ServiceProvider.GetRequiredService<IInventoryReserver>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => reserver.ReserveAsync(
            Guid.NewGuid(), [new ReservationLine(variant, 1)], env.Clock.UtcNow.AddMinutes(15), TestContext.Current.CancellationToken));
        Assert.Equal((3, 0), await InventoryEnv.ReadAsync(env, variant));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_quantity_of_zero_or_less_is_a_caller_bug(int quantity)
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        await Assert.ThrowsAsync<ArgumentException>(() => InventoryEnv.ReserveAsync(env, Guid.NewGuid(), [new ReservationLine(Guid.NewGuid(), quantity)]));
    }

    [Fact]
    public async Task No_lines_or_a_duplicate_variant_is_a_caller_bug()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid v = Guid.NewGuid();
        await Assert.ThrowsAsync<ArgumentException>(() => InventoryEnv.ReserveAsync(env, Guid.NewGuid(), []));
        await Assert.ThrowsAsync<ArgumentException>(() => InventoryEnv.ReserveAsync(env, Guid.NewGuid(), [new ReservationLine(v, 1), new ReservationLine(v, 2)]));
    }
}
