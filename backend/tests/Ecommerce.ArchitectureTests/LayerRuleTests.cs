using System.Text.RegularExpressions;

namespace Ecommerce.ArchitectureTests;

public partial class LayerRuleTests
{
    [Fact]
    public void Domain_types_depend_only_on_System_SharedKernel_and_their_own_domain() =>
        AssertLayerTypes("Domain");

    [Fact]
    public void Application_types_depend_only_on_domain_application_contracts_and_SharedKernel() =>
        AssertLayerTypes("Application");

    [Fact]
    public void Api_types_never_depend_on_infrastructure_or_ef() =>
        AssertLayerTypes("Api");

    [Fact]
    public void Layer_source_files_never_name_a_forbidden_namespace()
    {
        var modules = SolutionLayout.ExpectedModules;
        var violations = new List<string>();

        foreach (var impl in SolutionLayout.Modules(ProjectKind.ModuleImpl))
        {
            var module = impl.Node.Module!;
            foreach (var layer in new[] { "Domain", "Application", "Api" })
            {
                var files = SolutionLayout.SourceFiles(Path.Combine(impl.Directory, layer)).ToList();
                if (module == "Catalog")
                {
                    Assert.True(files.Count > 0, $"No {layer} source files found for Catalog; the scan is vacuous.");
                }
                foreach (var file in files)
                {
                    violations.AddRange(SourceRules.Violations(
                        module, layer, Path.GetRelativePath(SolutionLayout.Root, file), File.ReadAllLines(file), modules));
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Module_sources_declare_no_global_usings_and_namespaces_match_folders()
    {
        var problems = new List<string>();
        var scanned = 0;

        foreach (var project in SolutionLayout.Projects.Where(p => p.Node.Kind is ProjectKind.ModuleImpl or ProjectKind.ModuleContracts))
        {
            foreach (var file in SolutionLayout.SourceFiles(project.Directory))
            {
                scanned++;
                var relative = Path.GetRelativePath(SolutionLayout.Root, file);
                var folders = Path.GetRelativePath(project.Directory, Path.GetDirectoryName(file)!)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Where(s => s != ".");
                var expected = string.Join('.', new[] { project.Name }.Concat(folders));

                var text = File.ReadAllText(file);
                if (GlobalUsing().IsMatch(text))
                {
                    problems.Add($"{relative}: declares a global using");
                }

                var namespaces = NamespaceDeclaration().Matches(text).Select(m => m.Groups[1].Value).ToList();
                if (namespaces.Count == 0 || namespaces.Any(n => n != expected))
                {
                    problems.Add($"{relative}: namespace [{string.Join(", ", namespaces)}] does not match folder, expected {expected}");
                }
            }
        }

        Assert.True(scanned > 0, "No module source files scanned; the scan is vacuous.");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static void AssertLayerTypes(string layer)
    {
        var violations = new List<string>();
        foreach (var impl in SolutionLayout.Modules(ProjectKind.ModuleImpl))
        {
            var module = impl.Node.Module!;
            var assembly = impl.Assembly;
            var prefix = $"Ecommerce.{module}.{layer}";
            var types = assembly.GetTypes().Where(t => LayerRules.InNamespace(t.Namespace, prefix)).ToList();
            if (module == "Catalog")
            {
                Assert.True(types.Count > 0, $"No Catalog {layer} types found; the check is vacuous.");
            }
            foreach (var type in types)
            {
                violations.AddRange(LayerRules.Violations(module, layer, assembly, type.FullName ?? type.Name, TypeDependencies.Of(type)));
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [GeneratedRegex(@"^\s*global\s+using\b", RegexOptions.Multiline)]
    private static partial Regex GlobalUsing();

    [GeneratedRegex(@"^\s*namespace\s+([A-Za-z0-9_.]+)", RegexOptions.Multiline)]
    private static partial Regex NamespaceDeclaration();
}
