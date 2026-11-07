using System.Globalization;
using Ecommerce.Cart.Application;
using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Cart.Api;

// Wire shapes (arch 9, Cart contract). Money is a decimal string. The enums serialise as their names.
internal sealed record MoneyResponse(string Amount, string Currency)
{
    public static MoneyResponse From(decimal amount, string currency) =>
        new(amount.ToString("0.00##", CultureInfo.InvariantCulture), currency);
}

internal sealed record CartImageResponse(string Url, string AltText);

internal sealed record CartLineResponse(
    Guid VariantId, string ProductSlug, string ProductName, string VariantName, CartImageResponse? Image,
    int Quantity, MoneyResponse UnitPrice, MoneyResponse LineTotal, StockStatus StockStatus, IReadOnlyList<CartIssue> Issues);

internal sealed record CartResponse(IReadOnlyList<CartLineResponse> Items, MoneyResponse Subtotal);

internal sealed record MergeLineResultResponse(Guid VariantId, MergeOutcome Outcome, int Quantity, MergeReason? Reason);

internal sealed record MergeResponse(CartResponse Cart, IReadOnlyList<MergeLineResultResponse> Results);

internal static class CartResponseMapping
{
    public static CartResponse ToResponse(this CartView cart) =>
        new(cart.Items.Select(i => new CartLineResponse(
                i.VariantId, i.ProductSlug, i.ProductName, i.VariantName,
                i.Image is null ? null : new CartImageResponse(i.Image.Url, i.Image.AltText),
                i.Quantity, MoneyResponse.From(i.UnitPrice, i.Currency), MoneyResponse.From(i.LineTotal, i.Currency),
                i.StockStatus, i.Issues)).ToList(),
            MoneyResponse.From(cart.Subtotal, cart.Currency));

    public static MergeResponse ToResponse(this MergeResult result) =>
        new(result.Cart.ToResponse(),
            result.Results.Select(r => new MergeLineResultResponse(r.VariantId, r.Outcome, r.Quantity, r.Reason)).ToList());
}
