using FluentValidation;

namespace Ecommerce.Orders.Api;

// Only an address and a method code travel (arch 9 checkout contract). No price, total, tax, shipping amount or status
// member exists, so strict binding turns an extra one into a 400 (rule 1).
internal sealed record ShippingAddressRequest(
    string? FullName, string? Line1, string? Line2, string? City, string? Region, string? PostalCode, string? Country);

internal sealed record CheckoutRequest(ShippingAddressRequest? ShippingAddress, string? ShippingMethodCode);

internal static class IdempotencyKeys
{
    public const int MaxLength = 100;

    public static bool IsValid(string? key) => key is not null && key.Trim().Length is >= 1 and <= MaxLength;
}

internal sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public const int MaxName = 100;
    public const int MaxPostalCode = 20;

    public CheckoutRequestValidator()
    {
        RuleFor(r => r.ShippingMethodCode).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Choose a shipping method.");
        RuleFor(r => r.ShippingAddress).NotNull().WithMessage("Enter a shipping address.");
        When(r => r.ShippingAddress is not null, () =>
        {
            RuleFor(r => r.ShippingAddress!.FullName).Must(Required(MaxName)).WithMessage($"Enter a full name of 1 to {MaxName} characters.");
            RuleFor(r => r.ShippingAddress!.Line1).Must(Required(MaxName)).WithMessage($"Enter an address of 1 to {MaxName} characters.");
            RuleFor(r => r.ShippingAddress!.City).Must(Required(MaxName)).WithMessage($"Enter a city of 1 to {MaxName} characters.");
            RuleFor(r => r.ShippingAddress!.PostalCode).Must(Required(MaxPostalCode)).WithMessage($"Enter a postal code of 1 to {MaxPostalCode} characters.");
            RuleFor(r => r.ShippingAddress!.Country).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Choose a country.");
            RuleFor(r => r.ShippingAddress!.Line2).Must(Optional(MaxName)).WithMessage($"Address line 2 can be at most {MaxName} characters.");
            RuleFor(r => r.ShippingAddress!.Region).Must(Optional(MaxName)).WithMessage($"Region can be at most {MaxName} characters.");
        });
    }

    private static Func<string?, bool> Required(int max) => v => v is not null && v.Trim().Length >= 1 && v.Trim().Length <= max;

    private static Func<string?, bool> Optional(int max) => v => v is null || v.Trim().Length <= max;
}
