using System.Reflection;
using Ecommerce.Api.Composition;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.ArchitectureTests;

public class ModuleModelOwnershipTests
{
    [Fact]
    public void Each_module_db_model_configures_only_its_own_domain_entities()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = "Host=unused" })
            .Build();
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddEcommerceModules(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        List<IModuleDbModel> models = provider.GetServices<IModuleDbModel>().ToList();
        Assert.NotEmpty(models);

        List<string> problems = new List<string>();
        int totalEntities = 0;
        foreach (IModuleDbModel? model in models)
        {
            Assembly assembly = model.GetType().Assembly;
            string module = assembly.GetName().Name!["Ecommerce.".Length..];
            ModelBuilder builder = new ModelBuilder();
            model.Configure(builder);

            foreach (IMutableEntityType entity in builder.Model.GetEntityTypes())
            {
                totalEntities++;
                Type clr = entity.ClrType;
                if (clr.Assembly != assembly || !LayerRules.InNamespace(clr.Namespace, $"Ecommerce.{module}.Domain"))
                {
                    problems.Add($"{model.GetType().FullName} configures {clr.FullName}, which is not in Ecommerce.{module}.Domain");
                }
            }
        }

        Assert.True(totalEntities > 0, "No entities contributed by any module model; the check is vacuous.");
        Assert.True(models.Any(m => m.GetType().Assembly.GetName().Name == "Ecommerce.Catalog"), "Catalog contributes no IModuleDbModel.");
        Assert.True(models.Any(m => m.GetType().Assembly.GetName().Name == "Ecommerce.Identity"), "Identity contributes no IModuleDbModel.");
        Assert.True(models.Any(m => m.GetType().Assembly.GetName().Name == "Ecommerce.Orders"), "Orders contributes no IModuleDbModel.");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
