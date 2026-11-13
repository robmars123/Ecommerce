using Ecommerce.Payments.Application;
using Ecommerce.Payments.Contracts;
using Microsoft.Extensions.Options;
using Stripe;

namespace Ecommerce.Payments.Infrastructure;

/// <summary>
/// PaymentIntents and refunds through the official SDK (D19). Card only; the order, payment and refund ids ride in the metadata.
/// Any provider failure, or a missing key, becomes <see cref="PaymentProviderUnavailableException"/> and the caller retries.
/// </summary>
internal sealed class StripePaymentGateway(IOptions<StripeOptions> options, HttpClient? http = null) : IPaymentGateway
{
    private const string UnexpectedState = "payment_intent_unexpected_state";

    public async Task<CreatedIntent> CreateIntentAsync(IntentRequest request, CancellationToken ct)
    {
        PaymentIntentCreateOptions create = new()
        {
            Amount = request.AmountMinor,
            Currency = request.Currency.ToLowerInvariant(),
            AllowedPaymentMethodTypes = [Domain.PaymentMethods.Card],
            Metadata = new Dictionary<string, string>
            {
                ["order_id"] = request.OrderId.ToString(),
                ["payment_id"] = request.PaymentId.ToString(),
            },
        };
        PaymentIntent intent = await CallAsync(client => new PaymentIntentService(client).CreateAsync(create, new RequestOptions { IdempotencyKey = request.IdempotencyKey }, ct));
        return new CreatedIntent(intent.Id, intent.ClientSecret);
    }

    public async Task<string> GetClientSecretAsync(string providerPaymentId, CancellationToken ct)
    {
        PaymentIntent intent = await CallAsync(client => new PaymentIntentService(client).GetAsync(providerPaymentId, null, null, ct));
        return intent.ClientSecret;
    }

    public async Task<string> CreateRefundAsync(RefundCall call, CancellationToken ct)
    {
        RefundCreateOptions create = new()
        {
            PaymentIntent = call.ProviderPaymentId,
            Amount = call.AmountMinor,
            Metadata = new Dictionary<string, string> { ["refund_id"] = call.RefundId.ToString() },
        };
        Refund refund = await CallAsync(client => new Stripe.RefundService(client).CreateAsync(create, new RequestOptions { IdempotencyKey = call.IdempotencyKey }, ct));
        return refund.Id;
    }

    public async Task CancelIntentAsync(string providerPaymentId, CancellationToken ct)
    {
        try
        {
            await CallAsync(client => new PaymentIntentService(client).CancelAsync(providerPaymentId, null, null, ct));
        }
        catch (PaymentProviderUnavailableException ex) when (ex.InnerException is StripeException { StripeError.Code: UnexpectedState })
        {
            // Already canceled or already paid: nothing left to cancel. The paid case is the webhook's business.
        }
    }

    private async Task<T> CallAsync<T>(Func<StripeClient, Task<T>> call)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SecretKey))
        {
            throw new PaymentProviderUnavailableException("Stripe:SecretKey is not configured.");
        }
        try
        {
            // `http` is null in production (the SDK uses its own client); tests inject a handler to see the requests.
            return await call(new StripeClient(options.Value.SecretKey, httpClient: http is null ? null : new SystemNetHttpClient(http)));
        }
        catch (Exception ex) when (ex is StripeException or HttpRequestException or TaskCanceledException)
        {
            throw new PaymentProviderUnavailableException("The payment provider could not be reached.", ex);
        }
    }
}
