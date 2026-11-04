using Microsoft.EntityFrameworkCore;

namespace Ecommerce.ArchitectureTests;

// Proves each rule evaluator fails on a violation (and passes on a legal reference), using synthetic inputs only.
public class RuleEvaluatorSelfTests
{
    private static readonly ProjectNode Host = new("Ecommerce.Api", ProjectKind.Host, null);
    private static readonly ProjectNode Kernel = new("Ecommerce.SharedKernel", ProjectKind.SharedKernel, null);
    private static readonly ProjectNode Persistence = new("Ecommerce.Persistence", ProjectKind.Persistence, null);
    private static readonly ProjectNode CatalogImpl = new("Ecommerce.Catalog", ProjectKind.ModuleImpl, "Catalog");
    private static readonly ProjectNode InventoryImpl = new("Ecommerce.Inventory", ProjectKind.ModuleImpl, "Inventory");
    private static readonly ProjectNode InventoryContracts = new("Ecommerce.Inventory.Contracts", ProjectKind.ModuleContracts, "Inventory");
    private static readonly ProjectNode CatalogContracts = new("Ecommerce.Catalog.Contracts", ProjectKind.ModuleContracts, "Catalog");

    [Fact]
    public void Reference_rules_reject_a_module_referencing_another_module_implementation() =>
        Assert.NotEmpty(ReferenceRules.Violations(InventoryImpl, [CatalogImpl]));

    [Fact]
    public void Reference_rules_reject_contracts_referencing_persistence() =>
        Assert.NotEmpty(ReferenceRules.Violations(CatalogContracts, [Persistence]));

    [Fact]
    public void Reference_rules_reject_a_module_referencing_the_host() =>
        Assert.NotEmpty(ReferenceRules.Violations(CatalogImpl, [Host]));

    [Fact]
    public void Reference_rules_accept_a_module_referencing_another_modules_contracts()
    {
        Assert.Empty(ReferenceRules.Violations(CatalogImpl, [Kernel, Persistence, InventoryContracts, CatalogContracts]));
        Assert.NotEmpty(ReferenceRules.Violations(Kernel, [CatalogContracts]));
    }

    [Fact]
    public void Layer_rules_reject_a_domain_type_with_an_ef_dependency()
    {
        var assembly = typeof(RuleEvaluatorSelfTests).Assembly;

        Assert.NotEmpty(LayerRules.Violations("Catalog", "Domain", assembly, "Synthetic", [typeof(DbContext)]));
        Assert.Empty(LayerRules.Violations("Catalog", "Domain", assembly, "Synthetic", [typeof(string), typeof(List<Guid>)]));
    }

    [Fact]
    public void Source_scan_rejects_an_ef_using_in_a_domain_file()
    {
        string[] modules = ["Catalog", "Inventory"];

        Assert.NotEmpty(SourceRules.Violations("Catalog", "Domain", "Synthetic.cs",
            ["using Microsoft.EntityFrameworkCore;"], modules));
        Assert.NotEmpty(SourceRules.Violations("Catalog", "Application", "Synthetic.cs",
            ["using Ecommerce.Catalog.Infrastructure;"], modules));
        Assert.NotEmpty(SourceRules.Violations("Catalog", "Application", "Synthetic.cs",
            ["using Ecommerce.Inventory.Infrastructure;"], modules));
        Assert.NotEmpty(SourceRules.Violations("Catalog", "Api", "Synthetic.cs",
            ["using Ecommerce.Persistence;"], modules));
        Assert.Empty(SourceRules.Violations("Catalog", "Application", "Synthetic.cs",
            ["using Ecommerce.Catalog.Domain;", "using Ecommerce.Inventory.Contracts;", "using Ecommerce.SharedKernel.Time;"], modules));
    }

    [Fact]
    public void Source_scan_rejects_another_modules_contracts_in_a_domain_file()
    {
        string[] modules = ["Catalog", "Inventory"];

        Assert.NotEmpty(SourceRules.Violations("Catalog", "Domain", "Synthetic.cs",
            ["using Ecommerce.Inventory.Contracts;"], modules));
        Assert.NotEmpty(SourceRules.Violations("Catalog", "Domain", "Synthetic.cs",
            ["using Ecommerce.Catalog.Contracts;"], modules));
        Assert.Empty(SourceRules.Violations("Catalog", "Application", "Synthetic.cs",
            ["using Ecommerce.Inventory.Contracts;"], modules));
    }
}
