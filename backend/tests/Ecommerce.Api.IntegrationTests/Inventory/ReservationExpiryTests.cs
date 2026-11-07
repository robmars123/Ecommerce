using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests.Inventory;

public class ReservationExpiryTests(PostgresFixture postgres)
{
    private static async Task<int> SweepAsync(IdentityTestEnv env)
    {
        using IServiceScope scope = env.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReservationExpiryService>().SweepAsync(TestContext.Current.CancellationToken);
    }

    private static Task<long> CountAsync(IdentityTestEnv env, string status) =>
        InventoryEnv.CountAsync(env, "SELECT count(*) FROM inventory_reservations WHERE status = @s", ("s", status));

    [Fact]
    public async Task An_expired_Active_reservation_becomes_Expired_and_reserved_is_released_once()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid variant = await InventoryEnv.AddVariantAsync(env, "EXP-1");
        await InventoryEnv.SetStockAsync(env, variant, 5);
        Assert.True((await InventoryEnv.ReserveAsync(env, Guid.NewGuid(), [new ReservationLine(variant, 2)])).Succeeded);
        env.Clock.UtcNow = env.Clock.UtcNow.AddMinutes(16);

        Assert.Equal(1, await SweepAsync(env));
        Assert.Equal(0, await SweepAsync(env));

        Assert.Equal((5, 0), await InventoryEnv.ReadAsync(env, variant));
        Assert.Equal(1, await CountAsync(env, "Expired"));
        Assert.Equal(0, await CountAsync(env, "Active"));
    }

    [Fact]
    public async Task An_unexpired_reservation_is_untouched()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid variant = await InventoryEnv.AddVariantAsync(env, "EXP-2");
        await InventoryEnv.SetStockAsync(env, variant, 5);
        Assert.True((await InventoryEnv.ReserveAsync(env, Guid.NewGuid(), [new ReservationLine(variant, 2)])).Succeeded);
        env.Clock.UtcNow = env.Clock.UtcNow.AddMinutes(14);

        Assert.Equal(0, await SweepAsync(env));

        Assert.Equal((5, 2), await InventoryEnv.ReadAsync(env, variant));
        Assert.Equal(1, await CountAsync(env, "Active"));
    }

    [Fact]
    public async Task Only_the_due_orders_are_expired_when_several_share_a_variant_and_a_multi_line_order_releases_every_line()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid a = await InventoryEnv.AddVariantAsync(env, "EXP-A");
        Guid b = await InventoryEnv.AddVariantAsync(env, "EXP-B");
        await InventoryEnv.SetStockAsync(env, a, 10);
        await InventoryEnv.SetStockAsync(env, b, 10);
        DateTimeOffset now = env.Clock.UtcNow;
        await InventoryEnv.ReserveAsync(env, Guid.NewGuid(), [new ReservationLine(a, 2), new ReservationLine(b, 3)], now.AddMinutes(5));
        await InventoryEnv.ReserveAsync(env, Guid.NewGuid(), [new ReservationLine(a, 4)], now.AddMinutes(30));
        env.Clock.UtcNow = now.AddMinutes(10);

        Assert.Equal(2, await SweepAsync(env));

        Assert.Equal((10, 4), await InventoryEnv.ReadAsync(env, a));
        Assert.Equal((10, 0), await InventoryEnv.ReadAsync(env, b));
    }

    [Fact]
    public async Task Two_sweeps_at_the_same_time_release_each_reservation_exactly_once()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid variant = await InventoryEnv.AddVariantAsync(env, "EXP-3");
        await InventoryEnv.SetStockAsync(env, variant, 20);
        for (int i = 0; i < 6; i++)
        {
            Assert.True((await InventoryEnv.ReserveAsync(env, Guid.NewGuid(), [new ReservationLine(variant, 2)])).Succeeded);
        }
        env.Clock.UtcNow = env.Clock.UtcNow.AddMinutes(16);

        int[] swept = await Task.WhenAll(SweepAsync(env), SweepAsync(env)).WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

        Assert.Equal(6, swept.Sum());
        Assert.Equal((20, 0), await InventoryEnv.ReadAsync(env, variant));
        Assert.Equal(6, await CountAsync(env, "Expired"));
    }
}
