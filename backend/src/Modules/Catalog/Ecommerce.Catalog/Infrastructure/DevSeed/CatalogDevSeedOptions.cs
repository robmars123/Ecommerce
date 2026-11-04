namespace Ecommerce.Catalog.Infrastructure.DevSeed;

internal sealed class CatalogDevSeedOptions
{
    public const string EnabledKey = "Catalog:DevSeed:Enabled";

    public bool Enabled { get; set; }
}
