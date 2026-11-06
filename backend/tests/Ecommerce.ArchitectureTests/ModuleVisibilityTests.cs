using System.Reflection;
using System.Runtime.CompilerServices;
using Ecommerce.Api.Composition;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.ArchitectureTests;

public class ModuleVisibilityTests
{
    [Fact]
    public void A_module_implementation_exports_only_its_module_class()
    {
        List<BackendProject> impls = SolutionLayout.Modules(ProjectKind.ModuleImpl).ToList();
        Assert.NotEmpty(impls);

        List<string> problems = new List<string>();
        foreach (BackendProject? impl in impls)
        {
            string module = impl.Node.Module!;
            string expected = $"Ecommerce.{module}.{module}Module";
            List<string> exported = impl.Assembly.GetExportedTypes().Select(t => t.FullName ?? t.Name).Order().ToList();
            if (!exported.SequenceEqual([expected]))
            {
                problems.Add($"{impl.Name} exports [{string.Join(", ", exported)}], expected only [{expected}]");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void InternalsVisibleTo_is_granted_only_to_allowed_test_assemblies()
    {
        List<string> problems = new List<string>();
        foreach (BackendProject project in SolutionLayout.Projects)
        {
            HashSet<string> allowed = new HashSet<string> { "Ecommerce.Api.IntegrationTests" };
            if (project.Node.Module is { } module)
            {
                allowed.Add($"Ecommerce.{module}.Tests");
            }

            IEnumerable<string> granted = project.Assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
                .Select(a => new AssemblyName(a.AssemblyName).Name ?? "");
            foreach (string? name in granted.Where(n => !allowed.Contains(n)))
            {
                problems.Add($"{project.Name} grants InternalsVisibleTo to {name}");
            }
        }

        Assert.NotEmpty(SolutionLayout.Projects);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Controllers_live_only_in_a_module_Api_namespace()
    {
        List<string> problems = new List<string>();
        int controllers = 0;
        foreach (BackendProject project in SolutionLayout.Projects)
        {
            foreach (Type? type in project.Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(ControllerBase))))
            {
                controllers++;
                string? allowedNamespace = project.Node.Kind == ProjectKind.ModuleImpl ? $"Ecommerce.{project.Node.Module}.Api" : null;
                if (allowedNamespace is null || !LayerRules.InNamespace(type.Namespace, allowedNamespace))
                {
                    problems.Add($"{type.FullName} in {project.Name} is a controller outside Ecommerce.<Module>.Api of its own module");
                }
            }
        }

        Assert.True(controllers > 0, "No controllers found; the scan is vacuous.");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Host_composes_every_module_assembly()
    {
        List<string> expected = SolutionLayout.Modules(ProjectKind.ModuleImpl).Select(m => m.Name).Order().ToList();
        List<string> composed = ModuleComposition.ModuleAssemblies.Select(a => a.GetName().Name!).Order().ToList();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, composed);
    }
}
