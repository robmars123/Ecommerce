using Ecommerce.Notifications.Api;
using Ecommerce.Notifications.Application;
using Ecommerce.Notifications.Contracts;
using Ecommerce.Notifications.Infrastructure;
using Ecommerce.SharedKernel.Outbox;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ecommerce.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<NotificationsOptions>()
            .Configure<IConfiguration>((o, c) => o.DevPickupDirectory = c["Notifications:DevPickupDirectory"] ?? "")
            .Validate<IHostEnvironment>(
                (o, env) => string.IsNullOrWhiteSpace(o.DevPickupDirectory) || env.IsDevelopment(),
                "Notifications:DevPickupDirectory may only be set in Development.")
            .ValidateOnStart();

        services.AddSignalR();

        // Development with a configured pickup directory writes a file; everywhere else the sender throws (no provider yet).
        services.AddScoped<IEmailSender>(sp =>
        {
            bool configured = !string.IsNullOrWhiteSpace(sp.GetRequiredService<IOptions<NotificationsOptions>>().Value.DevPickupDirectory);
            return sp.GetRequiredService<IHostEnvironment>().IsDevelopment() && configured
                ? ActivatorUtilities.CreateInstance<DevPickupDirectoryEmailSender>(sp)
                : new NoProviderEmailSender();
        });
        services.AddScoped<IOutboxConsumer, OrderEmailConsumer>();
        services.AddScoped<IOutboxConsumer, OrderStatusHubConsumer>();
        return services;
    }

    /// <summary>Maps the live order status hub. Authentication is required on the hub itself.</summary>
    public static IEndpointRouteBuilder MapNotificationsHub(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<OrderStatusHub>(OrderStatusHub.Path);
        return endpoints;
    }
}
