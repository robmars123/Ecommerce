using System.Reflection;
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
        IReadOnlyList<string> modules = SolutionLayout.ExpectedModules;
        List<string> violations = new List<string>();

        foreach (BackendProject impl in SolutionLayout.Modules(ProjectKind.ModuleImpl))
        {
            string module = impl.Node.Module!;
            foreach (string? layer in new[] { "Domain", "Application", "Api" })
            {
                List<string> files = SolutionLayout.SourceFiles(Path.Combine(impl.Directory, layer)).ToList();
                if (module is "Catalog" or "Identity")
                {
                    Assert.True(files.Count > 0, $"No {layer} source files found for {module}; the scan is vacuous.");
                }
                foreach (string? file in files)
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
        List<string> problems = new List<string>();
        int scanned = 0;

        foreach (BackendProject? project in SolutionLayout.Projects.Where(p => p.Node.Kind is ProjectKind.ModuleImpl or ProjectKind.ModuleContracts))
        {
            foreach (string file in SolutionLayout.SourceFiles(project.Directory))
            {
                scanned++;
                string relative = Path.GetRelativePath(SolutionLayout.Root, file);
                IEnumerable<string> folders = Path.GetRelativePath(project.Directory, Path.GetDirectoryName(file)!)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Where(s => s != ".");
                string expected = string.Join('.', new[] { project.Name }.Concat(folders));

                string text = File.ReadAllText(file);
                if (GlobalUsing().IsMatch(text))
                {
                    problems.Add($"{relative}: declares a global using");
                }

                List<string> namespaces = NamespaceDeclaration().Matches(text).Select(m => m.Groups[1].Value).ToList();
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
        List<string> violations = new List<string>();
        foreach (BackendProject impl in SolutionLayout.Modules(ProjectKind.ModuleImpl))
        {
            string module = impl.Node.Module!;
            Assembly assembly = impl.Assembly;
            string prefix = $"Ecommerce.{module}.{layer}";
            List<Type> types = assembly.GetTypes().Where(t => LayerRules.InNamespace(t.Namespace, prefix)).ToList();
            if (module is "Catalog" or "Identity")
            {
                Assert.True(types.Count > 0, $"No {module} {layer} types found; the check is vacuous.");
            }
            foreach (Type? type in types)
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
