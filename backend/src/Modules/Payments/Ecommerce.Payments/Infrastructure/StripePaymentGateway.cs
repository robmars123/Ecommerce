using Ecommerce.Payments.Application;
using Ecommerce.Payments.Contracts;
using Microsoft.Extensions.Options;
using Stripe;

namespace Ecommerce.Payments.Infrastructure;

/// <summary>
/// PaymentIntents through the official SDK (D19). Card only; the order and payment ids ride in the metadata. Any provider
/// failure, or a missing key, becomes <see cref="PaymentProviderUnavailableException"/> and the order stays PendingPayment.
/// </summary>
internal sealed class StripePaymentGateway(IOptions<StripeOptions> options) : IPaymentGateway
{
    public async Task<CreatedIntent> CreateIntentAsync(IntentRequest request, CancellationToken ct)
    {
        PaymentIntentCreateOptions create = new()
        {
            Amount = request.AmountMinor,
            Currency = request.Currency.ToLowerInvariant(),
            AllowedPaymentMethodTypes = ["card"],
            Metadata = new Dictionary<string, string>
            {
                ["order_id"] = request.OrderId.ToString(),
                ["payment_id"] = request.PaymentId.ToString(),
            },
        };
        PaymentIntent intent = await CallAsync(service => service.CreateAsync(create, new RequestOptions { IdempotencyKey = request.IdempotencyKey }, ct));
        return new CreatedIntent(intent.Id, intent.ClientSecret);
    }

    public async Task<string> GetClientSecretAsync(string providerPaymentId, CancellationToken ct)
    {
        PaymentIntent intent = await CallAsync(service => service.GetAsync(providerPaymentId, null, null, ct));
        return intent.ClientSecret;
    }

    private async Task<PaymentIntent> CallAsync(Func<PaymentIntentService, Task<PaymentIntent>> call)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SecretKey))
        {
            throw new PaymentProviderUnavailableException("Stripe:SecretKey is not configured.");
        }
        try
        {
            return await call(new PaymentIntentService(new StripeClient(options.Value.SecretKey)));
        }
        catch (Exception ex) when (ex is StripeException or HttpRequestException or TaskCanceledException)
        {
            throw new PaymentProviderUnavailableException("The payment provider could not be reached.", ex);
        }
    }
}
