using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Inventory.Infrastructure.Persistence;

internal sealed class InventoryDbModel : IModuleDbModel
{
    public void Configure(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(InventoryDbModel).Assembly,
            t => t.Namespace == typeof(InventoryDbModel).Namespace);
}
