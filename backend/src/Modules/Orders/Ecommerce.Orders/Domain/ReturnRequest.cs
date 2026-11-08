using System.Text.Json;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Orders.Domain;

/// <summary>Arch 7.3 return machine: Requested to Approved or Declined; Approved to Refunded (by the refund webhook).</summary>
internal enum ReturnStatus
{
    Requested,
    Approved,
    Declined,
    Refunded,
}

internal sealed record ReturnItem(Guid VariantId, int Quantity);

/// <summary>A customer's request to return part of a Delivered order. Moves no money by itself.</summary>
internal sealed class ReturnRequest
{
    public const int MaxReasonLength = 500;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ReturnRequest() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string ItemsJson { get; private set; } = "[]";
    public string Reason { get; private set; } = "";
    public ReturnStatus Status { get; private set; }
    public string? AdminNote { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    public IReadOnlyList<ReturnItem> Items => JsonSerializer.Deserialize<List<ReturnItem>>(ItemsJson, Json) ?? [];

    public static ReturnRequest Create(Guid id, Guid orderId, IReadOnlyList<ReturnItem> items, string reason, DateTimeOffset now)
    {
        RequireReason(reason);
        if (items.Count == 0)
        {
            throw new DomainException("A return needs at least one item.");
        }
        return new ReturnRequest
        {
            Id = id, OrderId = orderId, ItemsJson = JsonSerializer.Serialize(items, Json), Reason = reason.Trim(),
            Status = ReturnStatus.Requested, CreatedAt = now,
        };
    }

    public void Approve(string note, DateTimeOffset now) => Decide(ReturnStatus.Approved, note, now);

    public void Decline(string note, DateTimeOffset now) => Decide(ReturnStatus.Declined, note, now);

    /// <summary>Approved to Refunded. Any other status is left alone: returns false.</summary>
    public bool MarkRefunded(DateTimeOffset now)
    {
        if (Status != ReturnStatus.Approved)
        {
            return false;
        }
        Status = ReturnStatus.Refunded;
        return true;
    }

    private void Decide(ReturnStatus to, string note, DateTimeOffset now)
    {
        RequireReason(note);
        if (Status != ReturnStatus.Requested)
        {
            throw new DomainException($"illegal-transition: a {Status} return cannot be {to}.");
        }
        Status = to;
        AdminNote = note.Trim();
        DecidedAt = now;
    }

    private static void RequireReason(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > MaxReasonLength)
        {
            throw new DomainException($"A reason of 1 to {MaxReasonLength} characters is required.");
        }
    }
}
