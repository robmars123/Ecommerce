using Ecommerce.Orders.Domain;
using FluentValidation;

namespace Ecommerce.Orders.Api;

internal sealed record ShipOrderRequest(string? CarrierCode, string? TrackingNumber);

internal sealed record StatusOverrideRequest(string? ToStatus, string? Reason);

internal sealed class ShipOrderRequestValidator : AbstractValidator<ShipOrderRequest>
{
    public const int MaxTrackingLength = 64;

    public ShipOrderRequestValidator()
    {
        RuleFor(r => r.CarrierCode).NotEmpty().WithMessage("Choose a carrier.");
        RuleFor(r => r.TrackingNumber)
            .Must(t => t is not null && t.Trim().Length is >= 1 and <= MaxTrackingLength)
            .WithMessage($"Tracking number must be 1 to {MaxTrackingLength} characters.")
            .Must(t => t is null || !t.Any(char.IsControl))
            .WithMessage("Tracking number must not contain control characters.");
    }
}

internal sealed class StatusOverrideRequestValidator : AbstractValidator<StatusOverrideRequest>
{
    public StatusOverrideRequestValidator()
    {
        RuleFor(r => r.ToStatus)
            .Must(s => TryParseStatus(s, out _))
            .WithMessage("Choose a valid order status.");
        RuleFor(r => r.Reason)
            .Must(r => r is not null && r.Trim().Length is >= 1 and <= Order.MaxReasonLength)
            .WithMessage($"Enter a reason of 1 to {Order.MaxReasonLength} characters.");
    }

    /// <summary>Names only: a number such as "3" is not a status.</summary>
    public static bool TryParseStatus(string? value, out OrderStatus status)
    {
        status = default;
        return value is not null && Enum.GetNames<OrderStatus>().Contains(value) && Enum.TryParse(value, out status);
    }
}

internal sealed record ReturnItemRequest(Guid? VariantId, int? Quantity);

internal sealed record ReturnRequestBody(IReadOnlyList<ReturnItemRequest>? Items, string? Reason);

internal sealed record DecisionRequest(string? Reason);

internal sealed class ReturnRequestBodyValidator : AbstractValidator<ReturnRequestBody>
{
    public const int MaxLines = 100;

    public ReturnRequestBodyValidator()
    {
        RuleFor(r => r.Items)
            .Must(i => i is not null && i.Count is >= 1 and <= MaxLines)
            .WithMessage($"Choose 1 to {MaxLines} items to return.");
        RuleForEach(r => r.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.VariantId).NotNull().WithMessage("Each item needs a variantId.");
            item.RuleFor(i => i.Quantity).NotNull().GreaterThanOrEqualTo(1).WithMessage("Each item needs a quantity of 1 or more.");
        });
        RuleFor(r => r.Reason)
            .Must(r => r is not null && r.Trim().Length is >= 1 and <= ReturnRequest.MaxReasonLength)
            .WithMessage($"Enter a reason of 1 to {ReturnRequest.MaxReasonLength} characters.");
    }
}

internal sealed class DecisionRequestValidator : AbstractValidator<DecisionRequest>
{
    public DecisionRequestValidator() =>
        RuleFor(r => r.Reason)
            .Must(r => r is not null && r.Trim().Length is >= 1 and <= ReturnRequest.MaxReasonLength)
            .WithMessage($"Enter a reason of 1 to {ReturnRequest.MaxReasonLength} characters.");
}

internal sealed record UnfulfillableRequest(string? Reason);

internal sealed class UnfulfillableRequestValidator : AbstractValidator<UnfulfillableRequest>
{
    public UnfulfillableRequestValidator() =>
        RuleFor(r => r.Reason)
            .Must(r => r is not null && r.Trim().Length is >= 1 and <= Order.MaxReasonLength)
            .WithMessage($"Enter a reason of 1 to {Order.MaxReasonLength} characters.");
}

/// <summary>Query of GET /admin/orders (arch 13.2 "Admin order list and actions"). Every member is optional.</summary>
internal sealed record AdminOrderListQuery(
    int? Page, int? PageSize, string? Status, DateOnly? From, DateOnly? To, string? Customer, string? OrderNumber);

internal sealed class AdminOrderListQueryValidator : AbstractValidator<AdminOrderListQuery>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxCustomerLength = 254;
    public const int MaxOrderNumberLength = 32;

    public AdminOrderListQueryValidator()
    {
        RuleFor(q => q.Page).Must(p => p is null or >= 1).WithMessage("Page must be 1 or more.");
        RuleFor(q => q.PageSize).Must(s => s is null or (>= 1 and <= MaxPageSize)).WithMessage($"Page size must be 1 to {MaxPageSize}.");
        RuleFor(q => q.Status)
            .Must(s => s is null || StatusOverrideRequestValidator.TryParseStatus(s, out _))
            .WithMessage("Choose a valid order status.");
        RuleFor(q => q.Customer)
            .Must(c => c is null || c.Trim().Length is >= 1 and <= MaxCustomerLength)
            .WithMessage($"Customer email must be 1 to {MaxCustomerLength} characters.");
        RuleFor(q => q.OrderNumber)
            .Must(n => n is null || n.Trim().Length is >= 1 and <= MaxOrderNumberLength)
            .WithMessage($"Order number must be 1 to {MaxOrderNumberLength} characters.");
        RuleFor(q => q.From)
            .Must((q, from) => from is null || q.To is null || from <= q.To)
            .WithMessage("From must be on or before To.");
    }
}
