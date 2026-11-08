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
