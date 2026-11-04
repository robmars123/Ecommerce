using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Persistence;

/// <summary>
/// A module's EF model contribution. Implemented internally by each module and registered by its Add&lt;M&gt;Module.
/// </summary>
public interface IModuleDbModel
{
    void Configure(ModelBuilder modelBuilder);
}
