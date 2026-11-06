using System.Xml.Linq;

namespace Ecommerce.ArchitectureTests;

public class ProjectReferenceRuleTests
{
    [Fact]
    public void Every_backend_project_is_classified()
    {
        List<string> unclassified = SolutionLayout.AllCsprojFiles
            .Where(f => SolutionLayout.Classify(Path.GetRelativePath(SolutionLayout.SrcDir, f)) is null)
            .Select(f => Path.GetRelativePath(SolutionLayout.Root, f))
            .ToList();
        Assert.True(unclassified.Count == 0,
            "Projects that fit no class (Host, SharedKernel, Persistence, ModuleImpl, ModuleContracts): " + string.Join(", ", unclassified));

        List<string> problems = new List<string>();
        foreach (BackendProject project in SolutionLayout.Projects)
        {
            string folder = Path.GetFileName(project.Directory);
            string file = Path.GetFileNameWithoutExtension(project.CsprojPath);
            if (folder != file || folder != project.Name)
            {
                problems.Add($"{project.CsprojPath}: folder '{folder}', project file '{file}', expected '{project.Name}'");
            }
            if (XDocument.Load(project.CsprojPath).Descendants("AssemblyName").Any())
            {
                problems.Add($"{project.Name}: overrides AssemblyName; assembly name must equal the folder name");
            }
        }

        foreach (string module in SolutionLayout.ExpectedModules)
        {
            if (!SolutionLayout.Projects.Any(p => p.Node.Kind == ProjectKind.ModuleImpl && p.Node.Module == module))
            {
                problems.Add($"Module {module} has no Ecommerce.{module} project");
            }
            if (!SolutionLayout.Projects.Any(p => p.Node.Kind == ProjectKind.ModuleContracts && p.Node.Module == module))
            {
                problems.Add($"Module {module} has no Ecommerce.{module}.Contracts project");
            }
        }

        List<string> modulesFound = SolutionLayout.Projects.Where(p => p.Node.Module is not null).Select(p => p.Node.Module!).Distinct().Order().ToList();
        Assert.Equal(SolutionLayout.ExpectedModules.Order().ToList(), modulesFound);

        foreach (ProjectKind kind in new[] { ProjectKind.Host, ProjectKind.SharedKernel, ProjectKind.Persistence })
        {
            if (SolutionLayout.Projects.Count(p => p.Node.Kind == kind) != 1)
            {
                problems.Add($"Expected exactly one {kind} project");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Csproj_project_references_follow_the_module_rules()
    {
        Dictionary<string, BackendProject> byName = SolutionLayout.Projects.ToDictionary(p => p.Name);
        List<string> violations = new List<string>();

        foreach (BackendProject project in SolutionLayout.Projects)
        {
            List<ProjectNode> targets = new List<ProjectNode>();
            foreach (string include in ProjectReferences(project))
            {
                string name = Path.GetFileNameWithoutExtension(include.Replace('\\', '/'));
                if (byName.TryGetValue(name, out BackendProject? target))
                {
                    targets.Add(target.Node);
                }
                else
                {
                    violations.Add($"{project.Name} references {include}, which is not a classified backend project");
                }
            }
            violations.AddRange(ReferenceRules.Violations(project.Node, targets));
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void SharedKernel_and_contracts_have_no_package_or_framework_references()
    {
        List<BackendProject> projects = SolutionLayout.Projects
            .Where(p => p.Node.Kind is ProjectKind.SharedKernel or ProjectKind.ModuleContracts)
            .ToList();
        Assert.NotEmpty(projects);

        List<string> offenders = projects
            .Where(p =>
            {
                XDocument doc = XDocument.Load(p.CsprojPath);
                return doc.Descendants("PackageReference").Any() || doc.Descendants("FrameworkReference").Any();
            })
            .Select(p => p.Name)
            .ToList();

        Assert.True(offenders.Count == 0, "Package/framework references in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Module_projects_use_the_plain_sdk_and_declare_no_global_usings()
    {
        List<BackendProject> projects = SolutionLayout.Projects
            .Where(p => p.Node.Kind is ProjectKind.ModuleImpl or ProjectKind.ModuleContracts)
            .ToList();
        Assert.NotEmpty(projects);

        List<string> problems = new List<string>();
        foreach (BackendProject? project in projects)
        {
            XDocument doc = XDocument.Load(project.CsprojPath);
            string? sdk = (string?)doc.Root?.Attribute("Sdk");
            if (sdk != "Microsoft.NET.Sdk")
            {
                problems.Add($"{project.Name}: Sdk is '{sdk}', expected 'Microsoft.NET.Sdk'");
            }
            if (doc.Descendants("Using").Any())
            {
                problems.Add($"{project.Name}: declares <Using> items (global usings)");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Host_references_every_module_implementation()
    {
        BackendProject host = SolutionLayout.Projects.Single(p => p.Node.Kind == ProjectKind.Host);
        HashSet<string> referenced = ProjectReferences(host)
            .Select(i => Path.GetFileNameWithoutExtension(i.Replace('\\', '/')))
            .ToHashSet();

        List<string> missing = SolutionLayout.Modules(ProjectKind.ModuleImpl)
            .Where(m => !referenced.Contains(m.Name))
            .Select(m => m.Name)
            .ToList();

        Assert.NotEmpty(SolutionLayout.Modules(ProjectKind.ModuleImpl));
        Assert.True(missing.Count == 0, "Host does not reference: " + string.Join(", ", missing));
    }

    private static IEnumerable<string> ProjectReferences(BackendProject project) =>
        XDocument.Load(project.CsprojPath)
            .Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(i => i is not null)
            .Select(i => i!);
}
