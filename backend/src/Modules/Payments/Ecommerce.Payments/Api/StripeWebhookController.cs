using System.Text;
using Ecommerce.Payments.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Payments.Api;

/// <summary>
/// Stripe calls this, not a signed-in user: the signature is the credential (rule 5), so it is anonymous to the bearer
/// policy. The raw body is read untouched because the signature covers its exact bytes. Thin: read, call, map.
/// </summary>
[ApiController]
[Route("api/v1/webhooks/stripe")]
[AllowAnonymous]
internal sealed class StripeWebhookController(PaymentWebhookService webhook) : ControllerBase
{
    private const string SignatureHeader = "Stripe-Signature";

    [HttpPost("")]
    [RequestSizeLimit(262144)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using StreamReader reader = new(Request.Body, Encoding.UTF8);
        string body = await reader.ReadToEndAsync(ct);
        WebhookResult result = await webhook.HandleAsync(body, Request.Headers[SignatureHeader].FirstOrDefault(), ct);
        return result == WebhookResult.BadSignature
            ? Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid signature")
            : Ok();
    }
}
