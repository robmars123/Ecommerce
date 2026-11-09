using System.Reflection;
using Ecommerce.Admin;
using Ecommerce.Cart;
using Ecommerce.Catalog;
using Ecommerce.Identity;
using Ecommerce.Inventory;
using Ecommerce.Notifications;
using Ecommerce.Orders;
using Ecommerce.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace Ecommerce.Api.Composition;

public static class ModuleComposition
{
    public static readonly IReadOnlyList<Assembly> ModuleAssemblies =
        [typeof(AdminModule).Assembly,
         typeof(CartModule).Assembly,
         typeof(CatalogModule).Assembly,
         typeof(IdentityModule).Assembly,
         typeof(InventoryModule).Assembly,
         typeof(NotificationsModule).Assembly,
         typeof(OrdersModule).Assembly,
         typeof(PaymentsModule).Assembly];

    public static IServiceCollection AddEcommerceModules(this IServiceCollection services, IConfiguration configuration) =>
        services.AddAdminModule(configuration).AddCartModule(configuration).AddCatalogModule(configuration).AddIdentityModule(configuration).AddInventoryModule(configuration).AddNotificationsModule(configuration).AddOrdersModule(configuration).AddPaymentsModule(configuration);

    public static IMvcBuilder AddModuleControllers(this IMvcBuilder mvc) =>
        mvc.ConfigureApplicationPartManager(m =>
        {
            foreach (Assembly assembly in ModuleAssemblies)
            {
                if (!m.ApplicationParts.OfType<AssemblyPart>().Any(p => p.Assembly == assembly))
                {
                    m.ApplicationParts.Add(new AssemblyPart(assembly));
                }
            }
            m.FeatureProviders.Add(new ModuleControllerFeatureProvider(ModuleAssemblies));
        });
}
