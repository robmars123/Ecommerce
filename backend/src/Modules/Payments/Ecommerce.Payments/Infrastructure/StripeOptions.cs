using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Payments.Infrastructure;

/// <summary>Both values are secrets: environment variables or user secrets only (D18). Empty in appsettings.json.</summary>
internal sealed class StripeOptions
{
    public string SecretKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(WebhookSecret);
}

/// <summary>
/// Outside Development both secrets are required, so a deployment cannot start unable to take payments. In Development an
/// empty value only warns: checkout answers 503 and the webhook 400 until the owner sets test keys.
/// </summary>
internal sealed class StripeOptionsValidator(IHostEnvironment environment, ILogger<StripeOptionsValidator> logger) : IValidateOptions<StripeOptions>
{
    public ValidateOptionsResult Validate(string? name, StripeOptions options)
    {
        if (options.IsConfigured)
        {
            return ValidateOptionsResult.Success;
        }
        if (environment.IsDevelopment())
        {
            logger.LogWarning("Stripe:SecretKey or Stripe:WebhookSecret is empty; checkout answers 503 and the webhook 400 until both are set.");
            return ValidateOptionsResult.Success;
        }
        return ValidateOptionsResult.Fail("Stripe:SecretKey and Stripe:WebhookSecret are required outside Development. Set them with an environment variable or user secrets.");
    }
}
