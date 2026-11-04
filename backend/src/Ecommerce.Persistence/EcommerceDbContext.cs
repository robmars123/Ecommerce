using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Persistence;

public sealed class EcommerceDbContext : DbContext
{
    private readonly IReadOnlyList<IModuleDbModel> _modules;

    public EcommerceDbContext(DbContextOptions<EcommerceDbContext> options, IEnumerable<IModuleDbModel> modules)
        : base(options)
    {
        _modules = modules.ToList();
        if (_modules.Count == 0)
        {
            throw new InvalidOperationException("No module models are registered; the EF model would be empty.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var module in _modules)
        {
            module.Configure(modelBuilder);
        }
    }
}
