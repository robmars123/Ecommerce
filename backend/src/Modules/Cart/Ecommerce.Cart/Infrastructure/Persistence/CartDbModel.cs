using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Cart.Infrastructure.Persistence;

internal sealed class CartDbModel : IModuleDbModel
{
    public void Configure(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CartDbModel).Assembly,
            t => t.Namespace == typeof(CartDbModel).Namespace);
}
