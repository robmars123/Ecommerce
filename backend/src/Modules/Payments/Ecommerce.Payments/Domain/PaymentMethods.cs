namespace Ecommerce.Payments.Domain;

/// <summary>The payment method types a PaymentIntent allows. Card is the only one, so any capture is a card.</summary>
internal static class PaymentMethods
{
    public const string Card = "card";
}
