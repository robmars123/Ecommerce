using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence;

internal sealed class CatalogDbModel : IModuleDbModel
{
    public void Configure(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CatalogDbModel).Assembly,
            t => t.Namespace == typeof(CatalogDbModel).Namespace);
}
