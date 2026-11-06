namespace Ecommerce.ArchitectureTests;

public class AssemblyReferenceRuleTests
{
    private static readonly string[] ForbiddenInKernelAndContracts =
    [
        "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "Serilog", "Stripe", "StackExchange.Redis",
    ];

    [Fact]
    public void Compiled_assembly_references_follow_the_module_rules()
    {
        Dictionary<string, BackendProject> byName = SolutionLayout.Projects.ToDictionary(p => p.Name);
        List<string> violations = new List<string>();

        foreach (BackendProject project in SolutionLayout.Projects)
        {
            List<ProjectNode> targets = project.Assembly.GetReferencedAssemblies()
                .Select(a => a.Name ?? "")
                .Where(n => n.StartsWith("Ecommerce.", StringComparison.Ordinal))
                .Distinct()
                .Select(n => byName.TryGetValue(n, out BackendProject? t)
                    ? t.Node
                    : throw new InvalidOperationException($"{project.Name} links unknown Ecommerce assembly {n}"))
                .ToList();
            violations.AddRange(ReferenceRules.Violations(project.Node, targets));
        }

        Assert.NotEmpty(SolutionLayout.Projects);
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void SharedKernel_and_contracts_reference_no_infrastructure_framework()
    {
        List<BackendProject> projects = SolutionLayout.Projects
            .Where(p => p.Node.Kind is ProjectKind.SharedKernel or ProjectKind.ModuleContracts)
            .ToList();
        Assert.NotEmpty(projects);

        List<string> offenders = projects
            .SelectMany(p => p.Assembly.GetReferencedAssemblies()
                .Select(a => a.Name ?? "")
                .Where(n => ForbiddenInKernelAndContracts.Any(f => n.StartsWith(f, StringComparison.Ordinal)))
                .Select(n => $"{p.Name} -> {n}"))
            .ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }
}
