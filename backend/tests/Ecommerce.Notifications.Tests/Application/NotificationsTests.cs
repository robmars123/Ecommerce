using System.Security.Claims;
using System.Text.Json;
using Ecommerce.Identity.Contracts;
using Ecommerce.Notifications.Api;
using Ecommerce.Notifications.Application;
using Ecommerce.Notifications.Contracts;
using Ecommerce.Notifications.Infrastructure;
using Ecommerce.Orders.Contracts;
using Ecommerce.SharedKernel.Outbox;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Notifications.Tests.Application;

public class OrderEmailConsumerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static OutboxMessage Message(string type, Guid? eventId = null) =>
        new(eventId ?? Guid.NewGuid(), type, "Order", OrderId, 2,
            JsonSerializer.Serialize(new OrderEventPayload(OrderId, UserId, "A", "B", DateTimeOffset.UnixEpoch), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            DateTimeOffset.UnixEpoch);

    private static OrderNotificationView View(OrderNotificationShipment? shipment = null) =>
        new("ORD-2026-000042", "USD", 25m, [new OrderNotificationLine("Black tee", 2, 10m)], shipment);

    private static (OrderEmailConsumer Consumer, RecordingEmailSender Sender, FakeUsers Users, FakeOrders Orders) Build()
    {
        FakeUsers users = new();
        users.Contacts[UserId] = new UserContact("ana@example.test", "Ana");
        FakeOrders orders = new();
        orders.Views[OrderId] = View();
        RecordingEmailSender sender = new();
        return (new OrderEmailConsumer(users, orders, sender), sender, users, orders);
    }

    [Fact]
    public async Task OrderPaid_sends_the_confirmation_with_the_idempotency_key_consumer_colon_event_id()
    {
        (OrderEmailConsumer consumer, RecordingEmailSender sender, _, _) = Build();
        Guid eventId = Guid.NewGuid();

        await consumer.HandleAsync(Message(OrderEventTypes.OrderPaid, eventId), Ct);

        EmailMessage mail = Assert.Single(sender.Sent);
        Assert.Equal("ana@example.test", mail.To);
        Assert.Equal($"notifications.order-email:{eventId}", mail.IdempotencyKey);
        Assert.Contains("ORD-2026-000042", mail.Subject);
        Assert.Contains("confirmed", mail.Subject);
        Assert.Contains("2 x Black tee  10.00 USD", mail.TextBody);
        Assert.Contains("Total: 25.00 USD", mail.TextBody);
    }

    [Fact]
    public async Task OrderShipped_sends_carrier_tracking_number_and_url()
    {
        (OrderEmailConsumer consumer, RecordingEmailSender sender, _, FakeOrders orders) = Build();
        orders.Views[OrderId] = View(new OrderNotificationShipment("Example Post", "TRK-9", "https://t.example/TRK-9"));

        await consumer.HandleAsync(Message(OrderEventTypes.OrderShipped), Ct);

        EmailMessage mail = Assert.Single(sender.Sent);
        Assert.Contains("has shipped", mail.Subject);
        Assert.Contains("Carrier: Example Post", mail.TextBody);
        Assert.Contains("Tracking number: TRK-9", mail.TextBody);
        Assert.Contains("Track it: https://t.example/TRK-9", mail.TextBody);
    }

    [Fact]
    public async Task A_shipped_email_without_a_tracking_url_omits_the_link_line()
    {
        (OrderEmailConsumer consumer, RecordingEmailSender sender, _, FakeOrders orders) = Build();
        orders.Views[OrderId] = View(new OrderNotificationShipment("Example Post", "TRK-9", null));

        await consumer.HandleAsync(Message(OrderEventTypes.OrderShipped), Ct);

        Assert.DoesNotContain("Track it", Assert.Single(sender.Sent).TextBody);
    }

    [Fact]
    public async Task A_missing_user_or_order_sends_nothing_and_does_not_throw()
    {
        (OrderEmailConsumer consumer, RecordingEmailSender sender, FakeUsers users, FakeOrders orders) = Build();
        users.Contacts.Clear();
        await consumer.HandleAsync(Message(OrderEventTypes.OrderPaid), Ct);
        users.Contacts[UserId] = new UserContact("ana@example.test", "Ana");
        orders.Views.Clear();
        await consumer.HandleAsync(Message(OrderEventTypes.OrderPaid), Ct);

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task A_sender_that_throws_propagates_so_the_runtime_retries()
    {
        (OrderEmailConsumer consumer, RecordingEmailSender sender, _, _) = Build();
        sender.Throw = new EmailProviderUnavailableException("down");

        await Assert.ThrowsAsync<EmailProviderUnavailableException>(() => consumer.HandleAsync(Message(OrderEventTypes.OrderPaid), Ct));
    }

    [Fact]
    public void It_consumes_only_OrderPaid_and_OrderShipped()
    {
        (OrderEmailConsumer consumer, _, _, _) = Build();

        Assert.Equal(new[] { "OrderPaid", "OrderShipped" }, consumer.EventTypes.Order());
        Assert.Equal("notifications.order-email", consumer.Name);
    }
}

public class OrderStatusHubTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static OutboxMessage Message(Guid orderId, Guid userId, string to) =>
        new(Guid.NewGuid(), "OrderStatusChanged", "Order", orderId, 1,
            JsonSerializer.Serialize(new OrderEventPayload(orderId, userId, "Paid", to, DateTimeOffset.UnixEpoch), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task A_message_goes_only_to_the_owners_group_with_the_order_id_and_new_status()
    {
        FakeHubContext hub = new();
        Guid order = Guid.NewGuid();

        await new OrderStatusHubConsumer(hub).HandleAsync(Message(order, UserId, "Shipped"), Ct);

        HubSend sent = Assert.Single(hub.Sent);
        Assert.Equal($"group:user:{UserId}", sent.Target);
        Assert.Equal("orderStatusChanged", sent.Method);
        OrderStatusChangedMessage payload = Assert.IsType<OrderStatusChangedMessage>(sent.Argument);
        Assert.Equal((order, "Shipped"), (payload.OrderId, payload.Status));
    }

    [Fact]
    public async Task Two_users_messages_go_to_two_different_groups()
    {
        FakeHubContext hub = new();
        Guid other = Guid.NewGuid();

        await new OrderStatusHubConsumer(hub).HandleAsync(Message(Guid.NewGuid(), UserId, "Paid"), Ct);
        await new OrderStatusHubConsumer(hub).HandleAsync(Message(Guid.NewGuid(), other, "Paid"), Ct);

        Assert.Equal([$"group:user:{UserId}", $"group:user:{other}"], hub.Sent.Select(s => s.Target));
    }

    [Fact]
    public void It_listens_to_all_three_order_events()
    {
        Assert.Equal(new[] { "OrderPaid", "OrderShipped", "OrderStatusChanged" }, new OrderStatusHubConsumer(new FakeHubContext()).EventTypes.Order());
    }

    [Fact]
    public async Task On_connect_the_hub_joins_the_group_of_the_callers_sub_claim()
    {
        FakeGroupManager groups = new();
        OrderStatusHub hub = new()
        {
            Context = new FakeCallerContext(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", UserId.ToString())], "test"))),
            Groups = groups,
        };

        await hub.OnConnectedAsync();

        Assert.Equal(("conn-1", $"user:{UserId}"), Assert.Single(groups.Added));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task A_connection_without_a_valid_sub_is_aborted_and_joins_no_group(string? sub)
    {
        FakeGroupManager groups = new();
        FakeCallerContext context = new(new ClaimsPrincipal(new ClaimsIdentity(sub is null ? [] : [new Claim("sub", sub)], "test")));
        OrderStatusHub hub = new() { Context = context, Groups = groups };

        await hub.OnConnectedAsync();

        Assert.True(context.Aborted);
        Assert.Empty(groups.Added);
    }
}

public class EmailSenderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ecommerce-mail-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private DevPickupDirectoryEmailSender Sender(string? directory = null) =>
        new(Options.Create(new NotificationsOptions { DevPickupDirectory = directory ?? _directory }), new FakeEnvironment(), NullLogger<DevPickupDirectoryEmailSender>.Instance);

    [Fact]
    public async Task The_dev_sender_writes_one_file_per_idempotency_key_and_a_resend_overwrites_it()
    {
        EmailMessage first = new("ana@example.test", "Hello", "first body", "notifications.order-email:abc");

        await Sender().SendAsync(first, Ct);
        await Sender().SendAsync(first with { TextBody = "second body" }, Ct);
        await Sender().SendAsync(first with { IdempotencyKey = "notifications.order-email:def" }, Ct);

        string[] files = Directory.GetFiles(_directory);
        Assert.Equal(2, files.Length);
        string text = await File.ReadAllTextAsync(Path.Combine(_directory, "notifications.order-email_abc.txt"), Ct);
        Assert.Contains("To: ana@example.test", text);
        Assert.Contains("second body", text);
        Assert.DoesNotContain("first body", text);
    }

    [Theory]
    [InlineData("a:b", "a_b.txt")]
    [InlineData("../evil\\path", ".._evil_path.txt")]
    [InlineData("ok-1.2", "ok-1.2.txt")]
    public void The_file_name_is_made_of_safe_characters_only(string key, string expected) =>
        Assert.Equal(expected, DevPickupDirectoryEmailSender.FileNameOf(key));

    [Fact]
    public async Task An_unwritable_directory_is_an_EmailProviderUnavailableException()
    {
        string blocker = Path.Combine(Path.GetTempPath(), "ecommerce-blocker-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(blocker, "a file where a directory is needed", Ct);
        try
        {
            await Assert.ThrowsAsync<EmailProviderUnavailableException>(
                () => Sender(Path.Combine(blocker, "sub")).SendAsync(new EmailMessage("a@b.test", "s", "b", "k"), Ct));
        }
        finally
        {
            File.Delete(blocker);
        }
    }

    [Fact]
    public async Task Without_a_provider_the_sender_throws_so_mail_retries_and_dead_letters()
    {
        await Assert.ThrowsAsync<EmailProviderUnavailableException>(
            () => new NoProviderEmailSender().SendAsync(new EmailMessage("a@b.test", "s", "b", "k"), Ct));
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
