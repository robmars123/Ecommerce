using Ecommerce.Admin.Application;
using Ecommerce.Admin.Infrastructure;
using Ecommerce.Admin.Infrastructure.Persistence;
using Ecommerce.Persistence;
using Ecommerce.SharedKernel.Projections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ecommerce.Admin;

public static class AdminModule
{
    public static IServiceCollection AddAdminModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleDbModel, AdminDbModel>());
        services.AddSingleton(new DashboardSettings(configuration["Store:Currency"] is { Length: > 0 } currency ? currency : "USD"));
        services.AddScoped<IProjector, DashboardProjector>();
        services.AddScoped<IDashboardStore, DashboardStore>();
        services.AddScoped<DashboardQueryService>();
        services.AddScoped<CustomerQueryService>();
        return services;
    }
}
