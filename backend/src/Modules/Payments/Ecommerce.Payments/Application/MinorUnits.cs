namespace Ecommerce.Payments.Application;

/// <summary>Amount to the provider's minor unit. Assumes a 2-decimal currency; anything else fails loudly (D5).</summary>
internal static class MinorUnits
{
    public static long From(decimal amount)
    {
        decimal minor = amount * 100m;
        if (minor != decimal.Truncate(minor) || minor < 0m)
        {
            throw new InvalidOperationException($"Amount {amount} is not a whole number of minor units.");
        }
        return (long)minor;
    }
}
