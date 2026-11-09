using System.Reflection;
using System.Text.Json;
using Ecommerce.Orders.Application;
using Ecommerce.Orders.Contracts;
using Ecommerce.Orders.Domain;
using Ecommerce.SharedKernel.Audit;
using Ecommerce.SharedKernel.Domain;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Orders.Tests.Application;

internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public Exception? FailOnSave { get; set; }
    public int Saves { get; private set; }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        Saves++;
        return FailOnSave is null ? Task.CompletedTask : Task.FromException(FailOnSave);
    }

    public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
}

internal sealed class FakeAudit : IAuditLog
{
    public List<AuditEntry> Entries { get; } = [];

    public void Add(AuditEntry entry) => Entries.Add(entry);
}

internal sealed class FakeOutbox : IOutbox
{
    public List<OutboxMessage> Messages { get; } = [];

    public void Add(OutboxMessage message) => Messages.Add(message);
}

internal sealed class FakeOrderReadStore : IOrderReadStore
{
    public Task<RecentOrderPage> ListRecentAsync(int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
    public Task<RecentOrderPage> ListForAdminAsync(AdminOrderFilter filter, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();

    public Task<int> CountPendingReturnsAsync(CancellationToken ct) => throw new NotSupportedException();

    public OrderDetailRow? Order { get; set; }

    public Task<OrderListRows> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct) => Task.FromResult(new OrderListRows([], 0));

    public Task<OrderDetailRow?> GetAsync(Guid orderId, CancellationToken ct) => Task.FromResult(Order?.Id == orderId ? Order : null);
}

internal sealed class FakeReturnStore : IReturnStore
{
    public List<ReturnRequest> Requests { get; } = [];

    public void Add(ReturnRequest request) => Requests.Add(request);

    public Task<ReturnRequest?> LockAsync(Guid id, CancellationToken ct) => Task.FromResult(Requests.FirstOrDefault(r => r.Id == id));

    public Task<ReturnRequest?> GetOpenAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult(Requests.FirstOrDefault(r => r.OrderId == orderId && r.Status is ReturnStatus.Requested or ReturnStatus.Approved));

    public Task<ReturnRows> ListAsync(ReturnStatus? status, int page, int pageSize, CancellationToken ct) => Task.FromResult(new ReturnRows([], 0));

    public Task<IReadOnlyList<ReturnRow>> ListForOrderAsync(Guid orderId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ReturnRow>>([]);

    public Task<ReturnRow?> GetRowAsync(Guid id, CancellationToken ct) => Task.FromResult<ReturnRow?>(null);
}

internal sealed class FakeOrderStore : IOrderStore
{
    public Order? Locked { get; set; }

    public Task<Order?> GetForUpdateAsync(Guid id, CancellationToken ct) => Task.FromResult(Locked);

    public void Add(Order order) => Locked = order;

    public Task<Order?> LockForUpdateAsync(Guid id, CancellationToken ct) => Task.FromResult(Locked?.Id == id ? Locked : null);

    public Task<long> NextOrderNumberAsync(CancellationToken ct) => Task.FromResult(1L);

    public Task<Guid?> GetOwnerAsync(Guid id, CancellationToken ct) => Task.FromResult(Locked?.Id == id ? Locked.UserId : (Guid?)null);
}

// Returns, full refund transitions and the one-event-per-transition helper (Slice 6 D5, D12, D13).
public class ReturnsAndRefundsTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Variant = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Order OrderIn(OrderStatus status, Guid? userId = null)
    {
        Order order = (Order)Activator.CreateInstance(typeof(Order), nonPublic: true)!;
        typeof(Order).GetProperty(nameof(Order.Id))!.SetValue(order, Guid.NewGuid());
        typeof(Order).GetProperty(nameof(Order.UserId))!.SetValue(order, userId ?? Owner);
        typeof(Order).GetProperty(nameof(Order.Status))!.SetValue(order, status);
        return order;
    }

    private static OrderDetailRow Row(OrderStatus status, Guid? userId = null, int quantity = 2) =>
        new(Guid.NewGuid(), userId ?? Owner, "ORD-1", Now, status, "USD", 20m, 0m, 5m, 0m, 25m, "standard", "Standard",
            new ShippingAddress("A", "1 St", null, "City", null, "12345", "US"),
            [new OrderItemRow(Variant, "SKU-1", "Black tee", 10m, quantity, 10m * quantity)], [], []);

    // ---- ReturnRequest domain ----

    [Fact]
    public void A_new_return_is_Requested_trims_its_reason_and_keeps_the_items()
    {
        ReturnRequest request = ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), [new ReturnItem(Variant, 2)], "  Too small  ", Now);

        Assert.Equal((ReturnStatus.Requested, "Too small", Now, null), (request.Status, request.Reason, request.CreatedAt, request.DecidedAt));
        Assert.Equal(new ReturnItem(Variant, 2), Assert.Single(request.Items));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_return_needs_a_reason(string reason) =>
        Assert.Throws<DomainException>(() => ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), [new ReturnItem(Variant, 1)], reason, Now));

    [Fact]
    public void A_return_reason_over_500_characters_and_an_empty_item_list_are_rejected()
    {
        Assert.Throws<DomainException>(() => ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), [new ReturnItem(Variant, 1)], new string('x', 501), Now));
        Assert.Throws<DomainException>(() => ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), [], "x", Now));
        Assert.Equal(500, ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), [new ReturnItem(Variant, 1)], new string('x', 500), Now).Reason.Length);
    }

    [Fact]
    public void Approve_and_decline_work_only_from_Requested_and_record_the_note_and_time()
    {
        ReturnRequest approved = ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), [new ReturnItem(Variant, 1)], "r", Now);
        ReturnRequest declined = ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), [new ReturnItem(Variant, 1)], "r", Now);

        approved.Approve(" ok ", Now.AddHours(1));
        declined.Decline("late", Now.AddHours(2));

        Assert.Equal((ReturnStatus.Approved, "ok", Now.AddHours(1)), (approved.Status, approved.AdminNote, approved.DecidedAt));
        Assert.Equal((ReturnStatus.Declined, "late"), (declined.Status, declined.AdminNote));
        Assert.Throws<DomainException>(() => approved.Decline("x", Now));
        Assert.Throws<DomainException>(() => declined.Approve("x", Now));
        Assert.Throws<DomainException>(() => approved.Approve("x", Now));
    }

    [Fact]
    public void A_decision_needs_a_note_and_a_rejected_decision_leaves_the_request_untouched()
    {
        ReturnRequest request = ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), [new ReturnItem(Variant, 1)], "r", Now);

        Assert.Throws<DomainException>(() => request.Approve("", Now));

        Assert.Equal((ReturnStatus.Requested, null, null), (request.Status, request.AdminNote, request.DecidedAt));
    }

    [Fact]
    public void Only_an_Approved_return_becomes_Refunded()
    {
        ReturnRequest requested = ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), [new ReturnItem(Variant, 1)], "r", Now);
        ReturnRequest approved = ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), [new ReturnItem(Variant, 1)], "r", Now);
        approved.Approve("ok", Now);

        Assert.False(requested.MarkRefunded(Now));
        Assert.True(approved.MarkRefunded(Now));
        Assert.False(approved.MarkRefunded(Now));
        Assert.Equal((ReturnStatus.Requested, ReturnStatus.Refunded), (requested.Status, approved.Status));
    }

    // ---- Order full refund ----

    [Theory]
    [InlineData("Paid")]
    [InlineData("Processing")]
    public void A_full_refund_moves_Paid_or_Processing_to_Refunded_with_one_history_row(string fromName)
    {
        OrderStatus from = Enum.Parse<OrderStatus>(fromName);
        Order order = OrderIn(from);

        order.RefundInFull(Admin, Now);

        Assert.Equal((OrderStatus.Refunded, 1), (order.Status, order.Version));
        OrderStatusChange change = Assert.Single(order.History);
        Assert.Equal((from, OrderStatus.Refunded, Admin), (change.FromStatus, change.ToStatus, change.ChangedBy));
    }

    [Fact]
    public void A_Delivered_order_goes_to_Returned_and_every_other_status_rejects_the_refund_transitions()
    {
        Order delivered = OrderIn(OrderStatus.Delivered);
        delivered.ReturnInFull(null, Now);

        Assert.Equal(OrderStatus.Returned, delivered.Status);
        foreach (OrderStatus status in new[] { OrderStatus.PendingPayment, OrderStatus.Shipped, OrderStatus.Cancelled, OrderStatus.Refunded, OrderStatus.Returned })
        {
            Order order = OrderIn(status);
            Assert.Throws<DomainException>(() => order.RefundInFull(Admin, Now));
            Assert.Empty(order.History);
        }
        Assert.Throws<DomainException>(() => OrderIn(OrderStatus.Paid).ReturnInFull(Admin, Now));
    }

    // ---- OrderEvents ----

    [Fact]
    public void Ship_writes_OrderShipped_and_every_other_transition_writes_OrderStatusChanged_built_from_the_history_row()
    {
        Order shipping = OrderIn(OrderStatus.Processing);
        shipping.Ship(Admin, "carrier", "TRK-1", Now);
        Order processing = OrderIn(OrderStatus.Paid);
        processing.Process(Admin, Now);

        OutboxMessage shipped = OrderEvents.For(shipping);
        OutboxMessage changed = OrderEvents.For(processing);

        Assert.Equal((OrderEventTypes.OrderShipped, "Order", shipping.Id, shipping.Version), (shipped.EventType, shipped.AggregateType, shipped.AggregateId, shipped.AggregateVersion));
        Assert.Equal(OrderEventTypes.OrderStatusChanged, changed.EventType);
        OrderEventPayload payload = JsonSerializer.Deserialize<OrderEventPayload>(changed.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal((processing.Id, Owner, "Paid", "Processing", Now), (payload.OrderId, payload.UserId, payload.FromStatus, payload.ToStatus, payload.OccurredAt));
    }

    [Fact]
    public void Two_events_for_two_transitions_have_different_ids_and_the_event_follows_the_latest_history_row()
    {
        Order order = OrderIn(OrderStatus.Paid);
        order.Process(Admin, Now);
        OutboxMessage first = OrderEvents.For(order);
        order.Ship(Admin, "carrier", "T", Now.AddMinutes(1));
        OutboxMessage second = OrderEvents.For(order);

        Assert.NotEqual(first.EventId, second.EventId);
        Assert.Equal(OrderEventTypes.OrderStatusChanged, first.EventType);
        Assert.Equal(OrderEventTypes.OrderShipped, second.EventType);
        Assert.Equal(2, second.AggregateVersion);
    }

    // ---- IOrderRefunds ----

    private sealed record RefundRig(OrderRefunds Refunds, FakeOrderStore Orders, FakeReturnStore Returns, FakeOutbox Outbox);

    private static RefundRig NewRefundRig(Order? order)
    {
        FakeOrderStore orders = new() { Locked = order };
        FakeReturnStore returns = new();
        FakeOutbox outbox = new();
        return new RefundRig(new OrderRefunds(orders, returns, new FakeSagaStore(), outbox, new FakeClock(Now)), orders, returns, outbox);
    }

    [Fact]
    public async Task LockForRefund_reports_status_owner_and_whether_an_approved_return_is_open()
    {
        Order order = OrderIn(OrderStatus.Delivered);
        RefundRig rig = NewRefundRig(order);
        ReturnRequest request = ReturnRequest.Create(Guid.NewGuid(), order.Id, [new ReturnItem(Variant, 1)], "r", Now);
        rig.Returns.Add(request);

        Assert.False((await rig.Refunds.LockForRefundAsync(order.Id, Ct))!.HasApprovedReturn);
        request.Approve("ok", Now);
        RefundOrderInfo info = (await rig.Refunds.LockForRefundAsync(order.Id, Ct))!;

        Assert.Equal(("Delivered", Owner, true), (info.Status, info.UserId, info.HasApprovedReturn));
        Assert.Null(await rig.Refunds.LockForRefundAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task ApplyFullRefund_moves_a_Paid_order_to_Refunded_with_one_event_and_marks_nothing_else()
    {
        Order order = OrderIn(OrderStatus.Paid);
        RefundRig rig = NewRefundRig(order);

        bool moved = await rig.Refunds.ApplyFullRefundAsync(order.Id, Guid.NewGuid(), Admin, Ct);

        Assert.True(moved);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal("OrderStatusChanged", Assert.Single(rig.Outbox.Messages).EventType);
    }

    [Fact]
    public async Task ApplyFullRefund_on_a_Delivered_order_returns_it_and_refunds_the_approved_return()
    {
        Order order = OrderIn(OrderStatus.Delivered);
        RefundRig rig = NewRefundRig(order);
        ReturnRequest request = ReturnRequest.Create(Guid.NewGuid(), order.Id, [new ReturnItem(Variant, 1)], "r", Now);
        request.Approve("ok", Now);
        rig.Returns.Add(request);

        Assert.True(await rig.Refunds.ApplyFullRefundAsync(order.Id, Guid.NewGuid(), Admin, Ct));

        Assert.Equal((OrderStatus.Returned, ReturnStatus.Refunded), (order.Status, request.Status));
    }

    [Theory]
    [InlineData("Shipped")]
    [InlineData("Cancelled")]
    [InlineData("Refunded")]
    public async Task ApplyFullRefund_changes_nothing_and_says_so_when_no_transition_applies(string statusName)
    {
        OrderStatus status = Enum.Parse<OrderStatus>(statusName);
        Order order = OrderIn(status);
        RefundRig rig = NewRefundRig(order);

        Assert.False(await rig.Refunds.ApplyFullRefundAsync(order.Id, Guid.NewGuid(), Admin, Ct));
        Assert.False(await NewRefundRig(null).Refunds.ApplyFullRefundAsync(Guid.NewGuid(), Guid.NewGuid(), Admin, Ct));

        Assert.Equal(status, order.Status);
        Assert.Empty(rig.Outbox.Messages);
    }

    [Fact]
    public async Task MarkReturnRefunded_marks_only_an_approved_open_return()
    {
        Order order = OrderIn(OrderStatus.Delivered);
        RefundRig rig = NewRefundRig(order);
        ReturnRequest request = ReturnRequest.Create(Guid.NewGuid(), order.Id, [new ReturnItem(Variant, 1)], "r", Now);
        rig.Returns.Add(request);

        await rig.Refunds.MarkReturnRefundedAsync(order.Id, Ct);
        Assert.Equal(ReturnStatus.Requested, request.Status);
        request.Approve("ok", Now);
        await rig.Refunds.MarkReturnRefundedAsync(order.Id, Ct);

        Assert.Equal(ReturnStatus.Refunded, request.Status);
        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    // ---- ReturnService ----

    private sealed record ReturnRig(ReturnService Service, FakeOrderReadStore Orders, FakeReturnStore Returns, FakeUnitOfWork Unit, FakeAudit Audit);

    private static ReturnRig NewReturnRig(OrderDetailRow? order)
    {
        FakeOrderReadStore orders = new() { Order = order };
        FakeReturnStore returns = new();
        FakeUnitOfWork unit = new();
        FakeAudit audit = new();
        return new ReturnRig(new ReturnService(orders, returns, unit, audit, new FakeClock(Now)), orders, returns, unit, audit);
    }

    [Fact]
    public async Task The_owner_of_a_Delivered_order_creates_a_return_and_it_is_saved_once()
    {
        OrderDetailRow order = Row(OrderStatus.Delivered);
        ReturnRig rig = NewReturnRig(order);

        ReturnRequestResult result = await rig.Service.RequestAsync(Owner, order.Id, [new ReturnItem(Variant, 2)], "Too small", Ct);

        Assert.Equal(rig.Returns.Requests[0].Id, Assert.IsType<ReturnRequestResult.Created>(result).ReturnId);
        Assert.Equal(1, rig.Unit.Saves);
    }

    [Fact]
    public async Task Someone_elses_order_is_Forbidden_an_unknown_one_NotFound_and_a_non_Delivered_one_NotDelivered()
    {
        OrderDetailRow order = Row(OrderStatus.Delivered);
        ReturnRig rig = NewReturnRig(order);
        ReturnItem[] items = [new ReturnItem(Variant, 1)];

        Assert.IsType<ReturnRequestResult.Forbidden>(await rig.Service.RequestAsync(Guid.NewGuid(), order.Id, items, "r", Ct));
        Assert.IsType<ReturnRequestResult.NotFound>(await rig.Service.RequestAsync(Owner, Guid.NewGuid(), items, "r", Ct));
        foreach (OrderStatus status in new[] { OrderStatus.Paid, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Returned, OrderStatus.PendingPayment })
        {
            ReturnRig other = NewReturnRig(Row(status));
            Assert.IsType<ReturnRequestResult.NotDelivered>(await other.Service.RequestAsync(Owner, other.Orders.Order!.Id, items, "r", Ct));
        }
        Assert.Empty(rig.Returns.Requests);
        Assert.Equal(0, rig.Unit.Saves);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public async Task A_quantity_outside_one_to_ordered_is_Invalid(int quantity)
    {
        OrderDetailRow order = Row(OrderStatus.Delivered, quantity: 2);
        ReturnRig rig = NewReturnRig(order);

        ReturnRequestResult result = await rig.Service.RequestAsync(Owner, order.Id, [new ReturnItem(Variant, quantity)], "r", Ct);

        Assert.Equal("items", Assert.IsType<ReturnRequestResult.Invalid>(result).Field);
        Assert.Empty(rig.Returns.Requests);
    }

    [Fact]
    public async Task An_item_not_on_the_order_a_repeated_item_and_a_blank_reason_are_Invalid()
    {
        OrderDetailRow order = Row(OrderStatus.Delivered);
        ReturnRig rig = NewReturnRig(order);

        Assert.Equal("items", Assert.IsType<ReturnRequestResult.Invalid>(await rig.Service.RequestAsync(Owner, order.Id, [new ReturnItem(Guid.NewGuid(), 1)], "r", Ct)).Field);
        Assert.Equal("items", Assert.IsType<ReturnRequestResult.Invalid>(await rig.Service.RequestAsync(Owner, order.Id, [new ReturnItem(Variant, 1), new ReturnItem(Variant, 1)], "r", Ct)).Field);
        Assert.Equal("reason", Assert.IsType<ReturnRequestResult.Invalid>(await rig.Service.RequestAsync(Owner, order.Id, [new ReturnItem(Variant, 1)], " ", Ct)).Field);
        Assert.Empty(rig.Returns.Requests);
    }

    [Fact]
    public async Task A_second_open_request_is_AlreadyOpen_and_so_is_a_lost_race_at_the_unique_index()
    {
        OrderDetailRow order = Row(OrderStatus.Delivered);
        ReturnRig rig = NewReturnRig(order);
        ReturnItem[] items = [new ReturnItem(Variant, 1)];
        await rig.Service.RequestAsync(Owner, order.Id, items, "r", Ct);

        Assert.IsType<ReturnRequestResult.AlreadyOpen>(await rig.Service.RequestAsync(Owner, order.Id, items, "r", Ct));

        ReturnRig racing = NewReturnRig(order);
        racing.Unit.FailOnSave = new UniqueConstraintViolationException("ux_return_requests_open_order_id", new Exception());
        Assert.IsType<ReturnRequestResult.AlreadyOpen>(await racing.Service.RequestAsync(Owner, order.Id, items, "r", Ct));
    }

    [Fact]
    public async Task Approve_audits_actor_before_after_and_note_and_a_second_decision_is_a_Conflict_with_no_second_audit_row()
    {
        ReturnRig rig = NewReturnRig(Row(OrderStatus.Delivered));
        ReturnRequest request = ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), [new ReturnItem(Variant, 1)], "r", Now);
        rig.Returns.Add(request);

        DecisionResult approved = await rig.Service.ApproveAsync(Admin, request.Id, "Within 30 days", Ct);
        DecisionResult again = await rig.Service.DeclineAsync(Admin, request.Id, "changed my mind", Ct);

        Assert.Equal(DecisionOutcome.Ok, approved.Outcome);
        Assert.Equal(DecisionOutcome.Conflict, again.Outcome);
        Assert.Equal(ReturnStatus.Approved, request.Status);
        AuditEntry audit = Assert.Single(rig.Audit.Entries);
        Assert.Equal(("return.approve", Admin, "return_request", request.Id, "Within 30 days"), (audit.Action, audit.ActorId, audit.EntityType, audit.EntityId, audit.Reason));
        Assert.Contains("Requested", audit.BeforeJson);
        Assert.Contains("Approved", audit.AfterJson);
    }

    [Fact]
    public async Task Decline_works_and_an_unknown_request_is_NotFound()
    {
        ReturnRig rig = NewReturnRig(Row(OrderStatus.Delivered));
        ReturnRequest request = ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), [new ReturnItem(Variant, 1)], "r", Now);
        rig.Returns.Add(request);

        Assert.Equal(DecisionOutcome.Ok, (await rig.Service.DeclineAsync(Admin, request.Id, "Past the window", Ct)).Outcome);
        Assert.Equal(DecisionOutcome.NotFound, (await rig.Service.DeclineAsync(Admin, Guid.NewGuid(), "x", Ct)).Outcome);

        Assert.Equal(ReturnStatus.Declined, request.Status);
        Assert.Equal("return.decline", Assert.Single(rig.Audit.Entries).Action);
    }

    // ---- OrderReader ----

    [Fact]
    public async Task The_notification_view_has_lines_totals_and_the_latest_shipment_with_a_tracking_url()
    {
        OrderDetailRow order = Row(OrderStatus.Shipped) with
        {
            Shipments = [new ShipmentRow("example-post", "OLD-1", Now, null), new ShipmentRow("example-post", "TRK-2", Now.AddDays(1), null)],
        };
        FakeOrderReadStore store = new() { Order = order };
        ShippingCarriers carriers = new([new Carrier("example-post", "Example Post", "https://t.example/{trackingNumber}")]);

        OrderNotificationView view = (await new OrderReader(store, carriers).GetNotificationViewAsync(order.Id, Ct))!;

        Assert.Equal(("ORD-1", "USD", 25m), (view.OrderNumber, view.Currency, view.GrandTotal));
        Assert.Equal(new OrderNotificationLine("Black tee", 2, 10m), Assert.Single(view.Lines));
        Assert.Equal(new OrderNotificationShipment("Example Post", "TRK-2", "https://t.example/TRK-2"), view.LatestShipment);
        Assert.Null(await new OrderReader(store, carriers).GetNotificationViewAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task An_unshipped_order_has_no_latest_shipment()
    {
        FakeOrderReadStore store = new() { Order = Row(OrderStatus.Paid) };

        OrderNotificationView view = (await new OrderReader(store, new ShippingCarriers([])).GetNotificationViewAsync(store.Order.Id, Ct))!;

        Assert.Null(view.LatestShipment);
    }
}

/// <summary>Slice 6b: OrderRefunds now also looks for the order's saga; these rigs have none.</summary>
internal sealed class FakeSagaStore : IFulfillmentSagaStore
{
    public Task<FulfillmentSaga?> GetByOrderAsync(Guid orderId, CancellationToken ct) => Task.FromResult<FulfillmentSaga?>(null);

    public void Add(FulfillmentSaga saga) { }
}
