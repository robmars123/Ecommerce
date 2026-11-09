using Ecommerce.Orders.Api;
using Ecommerce.Orders.Application;
using Ecommerce.Orders.Contracts;
using Ecommerce.Orders.Infrastructure;
using Ecommerce.Orders.Infrastructure.Persistence;
using Ecommerce.Persistence;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Orders;

public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleDbModel, OrdersDbModel>());

        services.AddOptions<ShippingCarriersOptions>()
            .Configure<IConfiguration>((o, c) => o.Carriers = c.GetSection("Shipping:Carriers").Get<List<CarrierOption>>() ?? [])
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ShippingCarriersOptions>, ShippingCarriersOptionsValidator>());
        services.AddSingleton(sp => new ShippingCarriers(
            sp.GetRequiredService<IOptions<ShippingCarriersOptions>>().Value.Carriers
                .Select(c => new Carrier(c.Code, c.Name, c.TrackingUrlTemplate))
                .ToList()));

        services.AddOptions<CheckoutOptions>()
            .Configure<IConfiguration>((o, c) =>
            {
                o.Methods = c.GetSection("Shipping:Methods").Get<List<ShippingMethodOption>>() ?? [];
                o.Countries = c.GetSection("Shipping:Countries").Get<List<string>>() ?? [];
                o.TaxRatePercent = c.GetValue("Tax:RatePercent", o.TaxRatePercent);
                o.Currency = c.GetValue("Store:Currency", o.Currency) ?? o.Currency;
                o.ReservationTtlMinutes = c.GetValue("Checkout:ReservationTtlMinutes", o.ReservationTtlMinutes);
            })
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CheckoutOptions>, CheckoutOptionsValidator>());
        services.AddSingleton(sp => new ShippingMethods(
            sp.GetRequiredService<IOptions<CheckoutOptions>>().Value.Methods
                .Select(m => new ShippingMethod(m.Code, m.Name, m.Amount, m.EstimatedDays)).ToList(),
            sp.GetRequiredService<IOptions<CheckoutOptions>>().Value.Countries.ToList()));
        services.AddSingleton(sp =>
        {
            CheckoutOptions options = sp.GetRequiredService<IOptions<CheckoutOptions>>().Value;
            return new CheckoutSettings(options.Currency, options.TaxRatePercent, options.ReservationTtlMinutes);
        });

        services.AddScoped<IOrderStore, OrderStore>();
        services.AddScoped<IOrderReadStore, OrderReadStore>();
        services.AddScoped<OrderQueryService>();
        services.AddScoped<OrderFulfillmentService>();
        services.AddScoped<IReturnStore, ReturnStore>();
        services.AddScoped<ReturnService>();
        services.AddScoped<IOrderRefunds, OrderRefunds>();
        services.AddScoped<IFulfillmentSagaStore, FulfillmentSagaStore>();
        services.AddScoped<FulfillmentSagaService>();
        services.AddScoped<IOrderReader, OrderReader>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<RetryPaymentService>();
        services.AddScoped<OrderPaymentHandler>();
        services.AddScoped<IOrderPayments>(sp => sp.GetRequiredService<OrderPaymentHandler>());
        services.AddScoped<IOrderExpiry>(sp => sp.GetRequiredService<OrderPaymentHandler>());
        services.AddScoped<IValidator<CheckoutRequest>, CheckoutRequestValidator>();
        services.AddScoped<IValidator<ShipOrderRequest>, ShipOrderRequestValidator>();
        services.AddScoped<IValidator<StatusOverrideRequest>, StatusOverrideRequestValidator>();
        services.AddScoped<IValidator<UnfulfillableRequest>, UnfulfillableRequestValidator>();
        services.AddScoped<IValidator<AdminOrderListQuery>, AdminOrderListQueryValidator>();
        services.AddScoped<IValidator<ReturnRequestBody>, ReturnRequestBodyValidator>();
        services.AddScoped<IValidator<DecisionRequest>, DecisionRequestValidator>();
        return services;
    }
}
