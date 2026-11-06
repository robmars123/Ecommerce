using System.Reflection;
using Ecommerce.Catalog;
using Ecommerce.Identity;
using Ecommerce.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace Ecommerce.Api.Composition;

public static class ModuleComposition
{
    public static readonly IReadOnlyList<Assembly> ModuleAssemblies =
        [typeof(CatalogModule).Assembly,
         typeof(IdentityModule).Assembly,
         typeof(InventoryModule).Assembly];

    public static IServiceCollection AddEcommerceModules(this IServiceCollection services, IConfiguration configuration) =>
        services.AddCatalogModule(configuration).AddIdentityModule(configuration).AddInventoryModule(configuration);

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
