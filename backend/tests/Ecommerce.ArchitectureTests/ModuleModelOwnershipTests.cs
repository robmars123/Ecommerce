using Ecommerce.Api.Composition;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.ArchitectureTests;

public class ModuleModelOwnershipTests
{
    [Fact]
    public void Each_module_db_model_configures_only_its_own_domain_entities()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = "Host=unused" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddEcommerceModules(configuration);
        using var provider = services.BuildServiceProvider();

        var models = provider.GetServices<IModuleDbModel>().ToList();
        Assert.NotEmpty(models);

        var problems = new List<string>();
        var totalEntities = 0;
        foreach (var model in models)
        {
            var assembly = model.GetType().Assembly;
            var module = assembly.GetName().Name!["Ecommerce.".Length..];
            var builder = new ModelBuilder();
            model.Configure(builder);

            foreach (var entity in builder.Model.GetEntityTypes())
            {
                totalEntities++;
                var clr = entity.ClrType;
                if (clr.Assembly != assembly || !LayerRules.InNamespace(clr.Namespace, $"Ecommerce.{module}.Domain"))
                {
                    problems.Add($"{model.GetType().FullName} configures {clr.FullName}, which is not in Ecommerce.{module}.Domain");
                }
            }
        }

        Assert.True(totalEntities > 0, "No entities contributed by any module model; the check is vacuous.");
        Assert.True(models.Any(m => m.GetType().Assembly.GetName().Name == "Ecommerce.Catalog"), "Catalog contributes no IModuleDbModel.");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
