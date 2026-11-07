using Ecommerce.Cart.Api;
using Ecommerce.Cart.Contracts;
using Ecommerce.Cart.Application;
using Ecommerce.Cart.Infrastructure;
using Ecommerce.Cart.Infrastructure.Persistence;
using Ecommerce.Persistence;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Cart;

public static class CartModule
{
    public static IServiceCollection AddCartModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleDbModel, CartDbModel>());

        services.AddOptions<CartOptions>()
            .Configure<IConfiguration>((o, c) =>
            {
                o.MaxQuantityPerLine = c.GetValue("Cart:MaxQuantityPerLine", o.MaxQuantityPerLine);
                o.Currency = c.GetValue("Store:Currency", o.Currency) ?? o.Currency;
            })
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CartOptions>, CartOptionsValidator>());
        services.AddSingleton(sp =>
        {
            CartOptions options = sp.GetRequiredService<IOptions<CartOptions>>().Value;
            return new CartSettings(options.MaxQuantityPerLine, options.Currency);
        });

        services.AddScoped<ICartStore, CartStore>();
        services.AddScoped<CartLines>();
        services.AddScoped<ICartReader>(sp => sp.GetRequiredService<CartLines>());
        services.AddScoped<ICartWriter>(sp => sp.GetRequiredService<CartLines>());
        services.AddScoped<CartViewBuilder>();
        services.AddScoped<CartService>();
        services.AddScoped<CartMergeService>();
        services.AddScoped<IValidator<AddCartItemRequest>, AddCartItemRequestValidator>();
        services.AddScoped<IValidator<SetCartItemRequest>, SetCartItemRequestValidator>();
        services.AddScoped<IValidator<MergeCartRequest>, MergeCartRequestValidator>();
        return services;
    }
}
