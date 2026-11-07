using System.Text.Json;
using Ecommerce.Payments.Application;
using Microsoft.Extensions.Options;
using Stripe;

namespace Ecommerce.Payments.Infrastructure;

/// <summary>
/// Checks the signature with the SDK, then reads only <c>id</c>, <c>type</c> and <c>data.object.id</c> (D7). Nothing here
/// deserializes into SDK event types, so another API version on the sending account cannot make verification throw.
/// </summary>
internal sealed class StripeWebhookVerifier(IOptions<StripeOptions> options) : IWebhookVerifier
{
    private const long ToleranceSeconds = 300;

    public bool TryVerify(string body, string? signatureHeader, DateTimeOffset now, out PaymentEvent? paymentEvent)
    {
        paymentEvent = null;
        string secret = options.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        try
        {
            EventUtility.ValidateSignature(body, signatureHeader, secret, ToleranceSeconds, now.ToUnixTimeSeconds());
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            string? eventId = root.GetProperty("id").GetString();
            string? type = root.GetProperty("type").GetString();
            if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(type))
            {
                return false;
            }

            PaymentEventKind kind = type switch
            {
                "payment_intent.succeeded" => PaymentEventKind.Succeeded,
                "payment_intent.payment_failed" => PaymentEventKind.Failed,
                "payment_intent.canceled" => PaymentEventKind.Canceled,
                _ => PaymentEventKind.Other,
            };
            string? intentId = kind == PaymentEventKind.Other ? null : root.GetProperty("data").GetProperty("object").GetProperty("id").GetString();
            if (kind != PaymentEventKind.Other && string.IsNullOrEmpty(intentId))
            {
                return false;
            }

            paymentEvent = new PaymentEvent(eventId, type, kind, intentId);
            return true;
        }
        catch (Exception ex) when (ex is StripeException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }
}
