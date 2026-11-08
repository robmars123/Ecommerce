using Ecommerce.Payments.Application;
using Ecommerce.Payments.Contracts;
using Ecommerce.Payments.Infrastructure;
using Ecommerce.Payments.Infrastructure.Persistence;
using Ecommerce.Persistence;
using Ecommerce.SharedKernel.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Payments;

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleDbModel, PaymentsDbModel>());

        services.AddOptions<StripeOptions>()
            .Configure<IConfiguration>((o, c) =>
            {
                o.SecretKey = c["Stripe:SecretKey"] ?? "";
                o.WebhookSecret = c["Stripe:WebhookSecret"] ?? "";
            })
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<StripeOptions>, StripeOptionsValidator>());

        services.AddScoped<IPaymentStore, PaymentStore>();
        services.AddScoped<IPaymentGateway, StripePaymentGateway>();
        services.AddScoped<IWebhookVerifier, StripeWebhookVerifier>();
        services.AddSingleton<IPaymentAlerts, LoggingPaymentAlerts>();
        services.AddScoped<IPaymentAttempts, PaymentAttempts>();
        services.AddScoped<IRefundState, RefundState>();
        services.AddScoped<PaymentWebhookService>();
        services.AddScoped<RefundService>();
        services.AddScoped<IOutboxConsumer, RefundRequestedConsumer>();
        services.AddScoped<IOutboxConsumer, IntentCancelConsumer>();
        return services;
    }
}
