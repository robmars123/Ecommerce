using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.Composition;

// Design-time only (dotnet-ef). Same composition as runtime; points at the non-secret local Compose database.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EcommerceDbContext>
{
    private const string LocalConnection = "Host=localhost;Port=5434;Database=ecommerce;Username=ecommerce";

    public EcommerceDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = LocalConnection })
            .Build();
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddEcommercePersistence();
        services.AddEcommerceModules(configuration);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<EcommerceDbContext>();
    }
}
