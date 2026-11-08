using System.Text.Json;
using Ecommerce.Orders.Domain;
using Ecommerce.SharedKernel.Audit;
using Ecommerce.SharedKernel.Domain;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Orders.Application;

/// <summary>Created | NotFound | Forbidden | NotDelivered | Invalid | AlreadyOpen (D12).</summary>
internal abstract record ReturnRequestResult
{
    private ReturnRequestResult() { }

    public sealed record Created(Guid ReturnId) : ReturnRequestResult;

    public sealed record NotFound : ReturnRequestResult;

    public sealed record Forbidden : ReturnRequestResult;

    public sealed record NotDelivered : ReturnRequestResult;

    public sealed record Invalid(string Field, string Message) : ReturnRequestResult;

    public sealed record AlreadyOpen : ReturnRequestResult;
}

internal enum DecisionOutcome
{
    Ok,
    NotFound,
    Conflict,
    Invalid,
}

internal sealed record DecisionResult(DecisionOutcome Outcome, string? Message = null);

/// <summary>
/// Returns (arch 4.7, 7.3, Slice 6 D12, D13). A request moves no money: Approved becomes Refunded only when a refund on the
/// order succeeds (IOrderRefunds). Decisions lock the request row and write an audit row in the same transaction.
/// </summary>
internal sealed class ReturnService(IOrderReadStore orders, IReturnStore returns, IUnitOfWork unitOfWork, IAuditLog audit, IClock clock)
{
    private sealed record ReturnAuditState(string Status);

    public async Task<ReturnRequestResult> RequestAsync(Guid userId, Guid orderId, IReadOnlyList<ReturnItem> items, string reason, CancellationToken ct)
    {
        OrderDetailRow? order = await orders.GetAsync(orderId, ct);
        if (order is null)
        {
            return new ReturnRequestResult.NotFound();
        }
        if (order.UserId != userId)
        {
            return new ReturnRequestResult.Forbidden();
        }
        if (order.Status != OrderStatus.Delivered)
        {
            return new ReturnRequestResult.NotDelivered();
        }
        if (items.Select(i => i.VariantId).Distinct().Count() != items.Count)
        {
            return new ReturnRequestResult.Invalid("items", "List each item once.");
        }
        foreach (ReturnItem item in items)
        {
            OrderItemRow? line = order.Items.FirstOrDefault(l => l.VariantId == item.VariantId);
            if (line is null)
            {
                return new ReturnRequestResult.Invalid("items", "Only items from this order can be returned.");
            }
            if (item.Quantity < 1 || item.Quantity > line.Quantity)
            {
                return new ReturnRequestResult.Invalid("items", $"Return 1 to {line.Quantity} of {line.Name}.");
            }
        }

        if (await returns.GetOpenAsync(orderId, ct) is not null)
        {
            return new ReturnRequestResult.AlreadyOpen();
        }
        DateTimeOffset now = clock.UtcNow;
        ReturnRequest request;
        try
        {
            request = ReturnRequest.Create(Guid.CreateVersion7(now), orderId, items, reason, now);
        }
        catch (DomainException ex)
        {
            return new ReturnRequestResult.Invalid("reason", ex.Message);
        }
        returns.Add(request);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // A double submit lost the race at the partial unique index.
            return new ReturnRequestResult.AlreadyOpen();
        }
        return new ReturnRequestResult.Created(request.Id);
    }

    public Task<DecisionResult> ApproveAsync(Guid actorId, Guid returnId, string note, CancellationToken ct) =>
        DecideAsync("return.approve", actorId, returnId, note, (r, n, now) => r.Approve(n, now), ct);

    public Task<DecisionResult> DeclineAsync(Guid actorId, Guid returnId, string note, CancellationToken ct) =>
        DecideAsync("return.decline", actorId, returnId, note, (r, n, now) => r.Decline(n, now), ct);

    public Task<ReturnRows> ListAsync(ReturnStatus? status, int page, int pageSize, CancellationToken ct) =>
        returns.ListAsync(status, page, pageSize, ct);

    private async Task<DecisionResult> DecideAsync(
        string action, Guid actorId, Guid returnId, string note, Action<ReturnRequest, string, DateTimeOffset> decide, CancellationToken ct) =>
        await unitOfWork.InTransactionAsync(async c =>
        {
            ReturnRequest? request = await returns.LockAsync(returnId, c);
            if (request is null)
            {
                return new DecisionResult(DecisionOutcome.NotFound);
            }

            DateTimeOffset now = clock.UtcNow;
            string before = JsonSerializer.Serialize(new ReturnAuditState(request.Status.ToString()));
            try
            {
                decide(request, note, now);
            }
            catch (DomainException ex)
            {
                return new DecisionResult(request.Status == ReturnStatus.Requested ? DecisionOutcome.Invalid : DecisionOutcome.Conflict, ex.Message);
            }
            audit.Add(new AuditEntry(
                actorId, action, "return_request", request.Id, before, JsonSerializer.Serialize(new ReturnAuditState(request.Status.ToString())), note, now));
            return new DecisionResult(DecisionOutcome.Ok);
        }, ct);
}
