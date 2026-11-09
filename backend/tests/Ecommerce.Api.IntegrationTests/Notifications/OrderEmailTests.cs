using System.Net;
using Ecommerce.Api.IntegrationTests.Checkout;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Refunds;
using Ecommerce.Notifications.Contracts;
using Ecommerce.Notifications.Infrastructure;
using Ecommerce.SharedKernel.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Serilog.Events;

namespace Ecommerce.Api.IntegrationTests.Notifications;

// Test 9 (arch 11.2): the email provider is down, the order stays Paid, the retry delivers, no duplicate. Plus the email content
// and "one outbox event per transition" for every Slice 6 transition site (Test 3 regression lives in WebhookTests, untouched).
public class OrderEmailTests(PostgresFixture postgres)
{
    private const string Consumer = "notifications.order-email";

    private static string Key() => "key-" + Guid.NewGuid().ToString("N");

    private static async Task<(RefundEnv E, Guid Order, Guid EventId)> PaidOrderAsync(PostgresFixture postgres)
    {
        RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid variant = await e.F.Cart.VariantAsync("MAIL-1", stock: 10);
        Guid order = await e.F.PlaceOrderAsync(variant, 2, Key());
        await e.F.PayAsync(order, "evt_pay_" + Guid.NewGuid().ToString("N"));
        Guid eventId = await e.F.ScalarAsync<Guid>("SELECT event_id FROM outbox_events WHERE event_type = 'OrderPaid' AND aggregate_id = @o", ("o", order));
        return (e, order, eventId);
    }

    private static async Task<(string Status, int Attempts)> RowAsync(RefundEnv e, Guid eventId) =>
        (await e.F.ScalarAsync<string>("SELECT status FROM processed_events WHERE consumer = @c AND event_id = @e", ("c", Consumer), ("e", eventId)),
         await e.F.ScalarAsync<int>("SELECT attempts FROM processed_events WHERE consumer = @c AND event_id = @e", ("c", Consumer), ("e", eventId)));

    [Fact]
    public async Task T9_a_provider_that_fails_twice_then_works_leaves_the_order_Paid_and_sends_exactly_one_email()
    {
        (RefundEnv e, Guid order, Guid eventId) = await PaidOrderAsync(postgres);
        await using RefundEnv env = e;
        e.Env.Sender.FailNext(2);

        await e.Env.DrainOutboxAsync(); // attempt 1 fails
        Assert.Equal("Paid", await e.F.OrderStatusAsync(order));
        Assert.Empty(e.Env.Sender.Sent);
        await e.Env.DrainOutboxAsync(); // still waiting for the backoff
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddSeconds(2);
        await e.Env.DrainOutboxAsync(); // attempt 2 fails
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddSeconds(4);
        await e.Env.DrainOutboxAsync(); // attempt 3 works
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddMinutes(10);
        await e.Env.DrainOutboxAsync(rounds: 2); // nothing left to do

        EmailMessage mail = Assert.Single(e.Env.Sender.Sent);
        Assert.Equal($"{Consumer}:{eventId}", mail.IdempotencyKey);
        Assert.Equal("a@example.test", mail.To);
        Assert.Equal(("Done", 3), await RowAsync(e, eventId));
        Assert.Equal(3, e.Env.Sender.Attempts);
        Assert.Equal("Paid", await e.F.OrderStatusAsync(order));
    }

    // Test 15 (arch 11.2, Slice 6b D20): delivery is at-least-once by design. The duplicate is proven harmless because the resend
    // carries the same idempotency key, and a sender that honours keys (the dev pickup directory here) keeps one email.
    [Fact]
    public async Task T15_a_send_whose_mark_is_lost_is_sent_again_with_the_same_key_and_the_dev_pickup_directory_keeps_one_file()
    {
        (RefundEnv e, Guid order, Guid eventId) = await PaidOrderAsync(postgres);
        await using RefundEnv env = e;
        string directory = Path.Combine(Path.GetTempPath(), "ecommerce-t15-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (IServiceScope scope = e.Env.Factory.Services.CreateScope())
            {
                IOutboxConsumer consumer = scope.ServiceProvider.GetServices<IOutboxConsumer>().Single(c => c.Name == Consumer);
                OutboxMessage message = new(
                    eventId, "OrderPaid", "Order", order, 1,
                    await e.F.ScalarAsync<string>("SELECT payload::text FROM outbox_events WHERE event_id = @e", ("e", eventId)), e.Env.Clock.UtcNow);
                await consumer.HandleAsync(message, TestContext.Current.CancellationToken); // the send happens; the crash comes before the mark
            }
            Assert.Single(e.Env.Sender.Sent);
            Assert.Equal(0, await e.F.CountAsync("SELECT count(*) FROM processed_events WHERE consumer = @c AND event_id = @e", ("c", Consumer), ("e", eventId)));

            await e.Env.DrainOutboxAsync(rounds: 2); // the runtime finds no mark and handles the event again

            Assert.Equal(2, e.Env.Sender.Attempts); // the second send happened ...
            EmailMessage kept = Assert.Single(e.Env.Sender.Sent); // ... and the provider, seeing the same key, kept one
            Assert.Equal($"{Consumer}:{eventId}", kept.IdempotencyKey);
            Assert.Equal(("Done", 1), await RowAsync(e, eventId));

            DevPickupDirectoryEmailSender dev = new(
                Options.Create(new NotificationsOptions { DevPickupDirectory = directory }),
                e.Env.Factory.Services.GetRequiredService<IHostEnvironment>(),
                NullLogger<DevPickupDirectoryEmailSender>.Instance);
            foreach (EmailMessage sent in new[] { kept, kept })
            {
                await dev.SendAsync(sent, TestContext.Current.CancellationToken);
            }
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task T9_six_failures_dead_letter_with_an_alert_and_the_order_stays_Paid()
    {
        (RefundEnv e, Guid order, Guid eventId) = await PaidOrderAsync(postgres);
        await using RefundEnv env = e;
        e.Env.Sender.FailNext(100);

        for (int i = 0; i < 8; i++)
        {
            await e.Env.DrainOutboxAsync();
            e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddMinutes(10);
        }

        Assert.Equal(("DeadLetter", 6), await RowAsync(e, eventId));
        Assert.Equal(6, e.Env.Sender.Attempts);
        Assert.Empty(e.Env.Sender.Sent);
        Assert.Equal("Paid", await e.F.OrderStatusAsync(order));
        Assert.Contains(e.Env.Logs.Events, l => l.Level == LogEventLevel.Fatal && l.RenderMessage().Contains("dead-lettered") && l.RenderMessage().Contains(eventId.ToString()));
    }

    [Fact]
    public async Task The_confirmation_email_names_the_order_the_lines_and_the_total()
    {
        (RefundEnv e, Guid order, _) = await PaidOrderAsync(postgres);
        await using RefundEnv env = e;
        string number = await e.F.ScalarAsync<string>("SELECT order_number FROM orders WHERE id = @o", ("o", order));

        await e.Env.DrainOutboxAsync();

        EmailMessage mail = Assert.Single(e.Env.Sender.Sent);
        Assert.Contains(number, mail.Subject);
        Assert.Contains("2 x ", mail.TextBody);
        Assert.Contains("25.00 USD", mail.TextBody); // 2 x 10.00 + 5.00 shipping
        Assert.Contains("Test User", mail.TextBody);
    }

    [Fact]
    public async Task The_shipped_email_has_the_carrier_the_tracking_number_and_the_tracking_url()
    {
        RefundEnv e = await RefundEnv.StartAsync(postgres);
        await using RefundEnv env = e;
        Guid order = await e.SeedOrderAsync("Processing");

        Assert.Equal(HttpStatusCode.NoContent, (await e.OrderActionAsync(order, "ship", new { carrierCode = Orders.OrdersEnv.Carrier, trackingNumber = "TRK-777" })).Status);
        await e.Env.DrainOutboxAsync(rounds: 2);

        EmailMessage mail = Assert.Single(e.Env.Sender.Sent);
        Assert.Contains("has shipped", mail.Subject);
        Assert.Contains("Carrier: Example Post", mail.TextBody);
        Assert.Contains("Tracking number: TRK-777", mail.TextBody);
        Assert.Contains("https://tracking.example.com/TRK-777", mail.TextBody);
    }

    [Fact]
    public async Task Every_transition_writes_exactly_one_outbox_event_and_only_ship_writes_OrderShipped()
    {
        RefundEnv e = await RefundEnv.StartAsync(postgres);
        await using RefundEnv env = e;
        Guid order = await e.SeedOrderAsync("Paid");
        Func<string, Task<long>> events = type => e.F.CountAsync("SELECT count(*) FROM outbox_events WHERE aggregate_id = @o AND event_type = @t", ("o", order), ("t", type));

        await e.OrderActionAsync(order, "process");
        Assert.Equal((1, 0), (await events("OrderStatusChanged"), await events("OrderShipped")));
        await e.OrderActionAsync(order, "ship", new { carrierCode = Orders.OrdersEnv.Carrier, trackingNumber = "T-1" });
        Assert.Equal((1, 1), (await events("OrderStatusChanged"), await events("OrderShipped")));
        await e.OrderActionAsync(order, "deliver");
        Assert.Equal((2, 1), (await events("OrderStatusChanged"), await events("OrderShipped")));
        // The seeded order has one history row (the seed); each of the three actions added one history row and one event.
        Assert.Equal(3, await e.F.CountAsync("SELECT count(*) FROM outbox_events WHERE aggregate_id = @o", ("o", order)));
        Assert.Equal(4, await e.F.CountAsync("SELECT count(*) FROM order_status_history WHERE order_id = @o", ("o", order)));
    }

    [Fact]
    public async Task An_override_a_full_refund_and_a_reservation_expiry_each_write_one_OrderStatusChanged()
    {
        RefundEnv e = await RefundEnv.StartAsync(postgres);
        await using RefundEnv env = e;
        Guid overridden = await e.SeedOrderAsync("Paid");
        Assert.Equal(HttpStatusCode.NoContent, (await e.OrderActionAsync(overridden, "status-override", new { toStatus = "Processing", reason = "picked by hand" })).Status);

        Guid refunded = await e.SeedOrderAsync("Paid");
        await e.RefundWebhookAsync(RefundEnv.RefundIdOf(await e.RefundAsync(refunded, "25.00")), "succeeded");

        Func<Guid, Task<long>> changed = id => e.F.CountAsync("SELECT count(*) FROM outbox_events WHERE aggregate_id = @o AND event_type = 'OrderStatusChanged'", ("o", id));
        Assert.Equal(1, await changed(overridden));
        Assert.Equal(1, await changed(refunded));

        // Expiry: an unpaid order whose reservation ran out is cancelled by the sweep.
        Guid variant = await e.F.Cart.VariantAsync("EXP-1", stock: 5);
        Guid expiring = await e.F.PlaceOrderAsync(variant, 1, Key());
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddHours(1);
        Assert.Equal(1, await e.F.SweepAsync());
        Assert.Equal("Cancelled", await e.F.OrderStatusAsync(expiring));
        Assert.Equal(1, await changed(expiring));
    }

    [Fact]
    public async Task The_intent_cancel_event_from_a_retry_reaches_the_provider_once()
    {
        RefundEnv e = await RefundEnv.StartAsync(postgres);
        await using RefundEnv env = e;
        Guid variant = await e.F.Cart.VariantAsync("CAN-1", stock: 5);
        Guid order = await e.F.PlaceOrderAsync(variant, 1, Key());
        string firstIntent = await e.F.IntentOfAsync(order);
        Assert.Equal(HttpStatusCode.OK, (await e.F.RetryAsync(e.TokenA, order, Key())).Status);

        await e.Env.DrainOutboxAsync(rounds: 2);

        Assert.Equal([firstIntent], e.F.Gateway.Cancels);
    }
}
