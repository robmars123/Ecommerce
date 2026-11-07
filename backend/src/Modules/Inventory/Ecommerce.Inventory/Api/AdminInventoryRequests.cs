using FluentValidation;

namespace Ecommerce.Inventory.Api;

internal sealed record AdjustStockRequest(Guid? VariantId, int? Delta, int? ExpectedOnHand, string? Reason);

internal sealed class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public const int MaxDelta = 1_000_000;
    public const int MaxReasonLength = 500;

    public AdjustStockRequestValidator()
    {
        RuleFor(r => r.VariantId).NotNull().NotEqual(Guid.Empty).WithMessage("Choose a variant.");
        RuleFor(r => r.Delta).NotNull().WithMessage("Enter a change.")
            .Must(d => d is null || (d != 0 && Math.Abs((long)d) <= MaxDelta))
            .WithMessage($"The change must not be 0 and must be within {MaxDelta:N0} either way.");
        RuleFor(r => r.ExpectedOnHand).NotNull().WithMessage("Enter the on-hand count you saw.")
            .Must(e => e is null or >= 0).WithMessage("The on-hand count cannot be negative.");
        RuleFor(r => r.Reason)
            .Must(r => r is not null && r.Trim().Length is >= 1 and <= MaxReasonLength)
            .WithMessage($"Enter a reason of 1 to {MaxReasonLength} characters.");
    }
}
