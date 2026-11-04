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
        var impls = SolutionLayout.Modules(ProjectKind.ModuleImpl).ToList();
        Assert.NotEmpty(impls);

        var problems = new List<string>();
        foreach (var impl in impls)
        {
            var module = impl.Node.Module!;
            var expected = $"Ecommerce.{module}.{module}Module";
            var exported = impl.Assembly.GetExportedTypes().Select(t => t.FullName ?? t.Name).Order().ToList();
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
        var problems = new List<string>();
        foreach (var project in SolutionLayout.Projects)
        {
            var allowed = new HashSet<string> { "Ecommerce.Api.IntegrationTests" };
            if (project.Node.Module is { } module)
            {
                allowed.Add($"Ecommerce.{module}.Tests");
            }

            var granted = project.Assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
                .Select(a => new AssemblyName(a.AssemblyName).Name ?? "");
            foreach (var name in granted.Where(n => !allowed.Contains(n)))
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
        var problems = new List<string>();
        var controllers = 0;
        foreach (var project in SolutionLayout.Projects)
        {
            foreach (var type in project.Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(ControllerBase))))
            {
                controllers++;
                var allowedNamespace = project.Node.Kind == ProjectKind.ModuleImpl ? $"Ecommerce.{project.Node.Module}.Api" : null;
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
        var expected = SolutionLayout.Modules(ProjectKind.ModuleImpl).Select(m => m.Name).Order().ToList();
        var composed = ModuleComposition.ModuleAssemblies.Select(a => a.GetName().Name!).Order().ToList();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, composed);
    }
}
