using System.Text.Json;
using Ecommerce.Payments.Application;
using Microsoft.Extensions.Options;
using Stripe;

namespace Ecommerce.Payments.Infrastructure;

/// <summary>
/// Checks the signature with the SDK, then reads only <c>id</c>, <c>type</c>, <c>data.object.id</c> and, for a success, <c>amount_received</c> and <c>currency</c> (D7). Nothing here
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

            if (type is "refund.updated" or "charge.refunded")
            {
                paymentEvent = new PaymentEvent(eventId, type, PaymentEventKind.Refund, null, ReadRefunds(type, root.GetProperty("data").GetProperty("object")));
                return true;
            }

            PaymentEventKind kind = type switch
            {
                "payment_intent.succeeded" => PaymentEventKind.Succeeded,
                "payment_intent.payment_failed" => PaymentEventKind.Failed,
                "payment_intent.canceled" => PaymentEventKind.Canceled,
                _ => PaymentEventKind.Other,
            };
            JsonElement intent = kind == PaymentEventKind.Other ? default : root.GetProperty("data").GetProperty("object");
            string? intentId = kind == PaymentEventKind.Other ? null : intent.GetProperty("id").GetString();
            if (kind != PaymentEventKind.Other && string.IsNullOrEmpty(intentId))
            {
                return false;
            }

            // A success is checked against the order's money (rules 1 and 5), so both fields must be there.
            long? amountReceived = null;
            string? currency = null;
            if (kind == PaymentEventKind.Succeeded)
            {
                if (!intent.TryGetProperty("amount_received", out JsonElement amount) || !amount.TryGetInt64(out long minor)
                    || !intent.TryGetProperty("currency", out JsonElement cur) || cur.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(cur.GetString()))
                {
                    return false;
                }
                amountReceived = minor;
                currency = cur.GetString();
            }

            paymentEvent = new PaymentEvent(eventId, type, kind, intentId, null, amountReceived, currency);
            return true;
        }
        catch (Exception ex) when (ex is StripeException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }

    // refund.updated carries the refund itself; charge.refunded carries the charge and, when expanded, its refunds.
    private static List<RefundNotice> ReadRefunds(string type, JsonElement obj)
    {
        if (type == "refund.updated")
        {
            return [ReadRefund(obj)];
        }
        List<RefundNotice> notices = [];
        if (obj.TryGetProperty("refunds", out JsonElement list) && list.ValueKind == JsonValueKind.Object
            && list.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Array)
        {
            notices.AddRange(data.EnumerateArray().Select(ReadRefund));
        }
        return notices;
    }

    private static RefundNotice ReadRefund(JsonElement refund)
    {
        Guid? ours = refund.TryGetProperty("metadata", out JsonElement meta) && meta.ValueKind == JsonValueKind.Object
                     && meta.TryGetProperty("refund_id", out JsonElement id) && Guid.TryParse(id.GetString(), out Guid parsed)
            ? parsed
            : null;
        string? providerId = refund.TryGetProperty("id", out JsonElement pid) ? pid.GetString() : null;
        RefundNoticeStatus status = (refund.TryGetProperty("status", out JsonElement s) ? s.GetString() : null) switch
        {
            "succeeded" => RefundNoticeStatus.Succeeded,
            "failed" or "canceled" => RefundNoticeStatus.Failed,
            _ => RefundNoticeStatus.Pending,
        };
        return new RefundNotice(ours, providerId, status);
    }
}
