using System.Globalization;
using System.Net;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Refunds;
using Ecommerce.Persistence.Projections;
using Ecommerce.SharedKernel.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests.Projections;

/// <summary>Slice 6b fixture: the Slice 6 users and gateway, orders paid through the real webhook, and projectors driven by hand (the worker is off).</summary>
internal sealed class ProjectionEnv : IAsyncDisposable
{
    private int _events;

    private ProjectionEnv(RefundEnv r) => R = r;

    public RefundEnv R { get; }
    public IdentityTestEnv Env => R.Env;
    public Checkout.CheckoutEnv F => R.F;

    public static async Task<ProjectionEnv> StartAsync(PostgresFixture postgres) => new(await RefundEnv.StartAsync(postgres));

    public ValueTask DisposeAsync() => R.DisposeAsync();

    /// <summary>Places an order for user A and pays it through the signed webhook. Returns the order id.</summary>
    public async Task<Guid> PaidOrderAsync(string sku, int quantity = 1, string? token = null)
    {
        Guid variant = await F.Cart.VariantAsync(sku, stock: 50);
        Guid orderId = await F.PlaceOrderAsync(variant, quantity, "pe-" + Guid.NewGuid().ToString("N"), token);
        await F.PayAsync(orderId, "evt_pe_" + Interlocked.Increment(ref _events) + "_" + Guid.NewGuid().ToString("N"));
        return orderId;
    }

    public Task<decimal> TotalOfAsync(Guid orderId) => F.ScalarAsync<decimal>("SELECT grand_total FROM orders WHERE id = @o", ("o", orderId));

    public static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Runs every projector until none has work left.</summary>
    public async Task DrainAsync()
    {
        for (int round = 0; round < 6; round++)
        {
            int handled = 0;
            using IServiceScope scope = Env.Factory.Services.CreateScope();
            ProjectionProcessor processor = scope.ServiceProvider.GetRequiredService<ProjectionProcessor>();
            foreach (IProjector projector in scope.ServiceProvider.GetServices<IProjector>())
            {
                handled += await processor.RunOnceAsync(projector, TestContext.Current.CancellationToken);
            }
            if (handled == 0)
            {
                return;
            }
        }
    }

    public Task<IdentityTestEnv.Api> DashboardAsync(string? token, string? period = null) =>
        Env.GetAsync("/api/v1/admin/dashboard" + (period is null ? "" : "?period=" + period), token);

    public async Task<IdentityTestEnv.Api> OkDashboardAsync(string period = "today")
    {
        IdentityTestEnv.Api r = await DashboardAsync(R.Admin, period);
        Assert.True(r.Status == HttpStatusCode.OK, r.Text + string.Join(" | ", Env.Logs.Events.Where(l => l.Exception is not null).Select(l => l.Exception!.GetType().Name + ": " + l.Exception.Message)));
        return r;
    }

    public static decimal AmountOf(System.Text.Json.JsonElement money) =>
        decimal.Parse(money.GetProperty("amount").GetString()!, CultureInfo.InvariantCulture);
}
