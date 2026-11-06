using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Identity.Infrastructure.Persistence;

internal sealed class IdentityDbModel : IModuleDbModel
{
    public void Configure(ModelBuilder modelBuilder)
    {
        // Case-insensitive email uniqueness is the database's job (a unique index on citext), not the application's.
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(IdentityDbModel).Assembly,
            t => t.Namespace == typeof(IdentityDbModel).Namespace);
    }
}
