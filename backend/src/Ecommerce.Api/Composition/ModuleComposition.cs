using System.Reflection;
using Ecommerce.Catalog;
using Ecommerce.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace Ecommerce.Api.Composition;

public static class ModuleComposition
{
    public static readonly IReadOnlyList<Assembly> ModuleAssemblies =
        [typeof(CatalogModule).Assembly, 
         typeof(InventoryModule).Assembly];

    public static IServiceCollection AddEcommerceModules(this IServiceCollection services, IConfiguration configuration) =>
        services.AddCatalogModule(configuration).AddInventoryModule(configuration);

    public static IMvcBuilder AddModuleControllers(this IMvcBuilder mvc) =>
        mvc.ConfigureApplicationPartManager(m =>
        {
            foreach (var assembly in ModuleAssemblies)
            {
                if (!m.ApplicationParts.OfType<AssemblyPart>().Any(p => p.Assembly == assembly))
                {
                    m.ApplicationParts.Add(new AssemblyPart(assembly));
                }
            }
            m.FeatureProviders.Add(new ModuleControllerFeatureProvider(ModuleAssemblies));
        });
}
