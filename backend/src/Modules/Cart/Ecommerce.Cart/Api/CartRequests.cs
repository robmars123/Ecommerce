using FluentValidation;

namespace Ecommerce.Cart.Api;

// No price, total or user field exists on any request (strict binding turns an extra one into a 400).
internal sealed record AddCartItemRequest(Guid? VariantId, int? Quantity);

internal sealed record SetCartItemRequest(int? Quantity);

internal sealed record MergeLineRequest(Guid? VariantId, int? Quantity);

internal sealed record MergeCartRequest(List<MergeLineRequest>? Lines);

internal sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public const int MaxRequestQuantity = 1000;

    public AddCartItemRequestValidator()
    {
        RuleFor(r => r.VariantId).NotNull().NotEqual(Guid.Empty).WithMessage("Choose an item.");
        RuleFor(r => r.Quantity).NotNull().WithMessage("Enter a quantity.")
            .Must(q => q is null or (>= 1 and <= MaxRequestQuantity)).WithMessage($"Quantity must be 1 to {MaxRequestQuantity}.");
    }
}

internal sealed class SetCartItemRequestValidator : AbstractValidator<SetCartItemRequest>
{
    public SetCartItemRequestValidator()
    {
        RuleFor(r => r.Quantity).NotNull().WithMessage("Enter a quantity.")
            .Must(q => q is null or (>= 0 and <= AddCartItemRequestValidator.MaxRequestQuantity)).WithMessage("Quantity cannot be negative.");
    }
}

internal sealed class MergeCartRequestValidator : AbstractValidator<MergeCartRequest>
{
    public const int MaxLines = 100;
    public const int MaxLineQuantity = 1000;
    public const int MaxKeyLength = 100;

    public MergeCartRequestValidator()
    {
        RuleFor(r => r.Lines).NotNull().WithMessage("Send the lines to merge.");
        RuleFor(r => r.Lines!.Count).InclusiveBetween(1, MaxLines).When(r => r.Lines is not null)
            .WithMessage($"Send 1 to {MaxLines} lines.").OverridePropertyName("lines");
        RuleFor(r => r.Lines!).Must(l => l.All(x => x is not null && x.VariantId is { } v && v != Guid.Empty))
            .When(r => r.Lines is not null).WithMessage("Every line needs an item.").OverridePropertyName("lines");
        RuleFor(r => r.Lines!).Must(l => l.All(x => x?.Quantity is >= 1 and <= MaxLineQuantity))
            .When(r => r.Lines is not null).WithMessage($"Every quantity must be 1 to {MaxLineQuantity}.").OverridePropertyName("lines");
        RuleFor(r => r.Lines!).Must(l => l.Where(x => x?.VariantId is not null).Select(x => x.VariantId).Distinct().Count() == l.Count(x => x?.VariantId is not null))
            .When(r => r.Lines is not null).WithMessage("Each item may appear once.").OverridePropertyName("lines");
    }

    public static bool IsValidKey(string? key) => key is not null && key.Trim().Length is >= 1 and <= MaxKeyLength;
}
