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
