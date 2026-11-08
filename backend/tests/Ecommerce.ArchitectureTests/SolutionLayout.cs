using System.Reflection;

namespace Ecommerce.ArchitectureTests;

internal enum ProjectKind
{
    Host,
    SharedKernel,
    Persistence,
    ModuleImpl,
    ModuleContracts,
}

/// <summary>A backend project as the rules see it. Module is set for ModuleImpl and ModuleContracts only.</summary>
internal sealed record ProjectNode(string Name, ProjectKind Kind, string? Module);

internal sealed record BackendProject(ProjectNode Node, string CsprojPath)
{
    public string Name => Node.Name;
    public string Directory => Path.GetDirectoryName(CsprojPath)!;
    public Assembly Assembly => Assembly.Load(Name);
}

/// <summary>Finds and classifies every project under backend/src. An unclassifiable project is a test failure.</summary>
internal static class SolutionLayout
{
    /// <summary>The modules the repository is expected to contain. Adding a module means adding it here.</summary>
    public static readonly IReadOnlyList<string> ExpectedModules = ["Cart", "Catalog", "Identity", "Inventory", "Notifications", "Orders", "Payments"];

    public static string Root { get; } = FindRoot();

    public static string SrcDir => Path.Combine(Root, "backend", "src");

    public static IReadOnlyList<string> AllCsprojFiles { get; } = Directory
        .EnumerateFiles(Path.Combine(FindRoot(), "backend", "src"), "*.csproj", SearchOption.AllDirectories)
        .Where(f => !IsBuildOutput(f))
        .OrderBy(f => f, StringComparer.Ordinal)
        .ToList();

    public static IReadOnlyList<BackendProject> Projects { get; } = AllCsprojFiles
        .Select(f => (File: f, Node: Classify(Path.GetRelativePath(Path.Combine(FindRoot(), "backend", "src"), f))))
        .Where(x => x.Node is not null)
        .Select(x => new BackendProject(x.Node!, x.File))
        .ToList();

    public static IEnumerable<BackendProject> Modules(ProjectKind kind) => Projects.Where(p => p.Node.Kind == kind);

    public static BackendProject Impl(string module) =>
        Projects.Single(p => p.Node.Kind == ProjectKind.ModuleImpl && p.Node.Module == module);

    public static IEnumerable<string> SourceFiles(string directory) => !Directory.Exists(directory) ? [] : Directory
        .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
        .Where(f => !IsBuildOutput(f))
        .OrderBy(f => f, StringComparer.Ordinal);

    /// <summary>relativeToSrc uses either slash. Returns null when the path fits no project class.</summary>
    public static ProjectNode? Classify(string relativeToSrc)
    {
        string[] parts = relativeToSrc.Replace('\\', '/').Split('/');
        switch (parts)
        {
            case ["Ecommerce.Api", "Ecommerce.Api.csproj"]:
                return new ProjectNode("Ecommerce.Api", ProjectKind.Host, null);
            case ["Ecommerce.SharedKernel", "Ecommerce.SharedKernel.csproj"]:
                return new ProjectNode("Ecommerce.SharedKernel", ProjectKind.SharedKernel, null);
            case ["Ecommerce.Persistence", "Ecommerce.Persistence.csproj"]:
                return new ProjectNode("Ecommerce.Persistence", ProjectKind.Persistence, null);
            case ["Modules", string module, string folder, string file]:
                if (file != folder + ".csproj")
                {
                    return null;
                }
                if (folder == $"Ecommerce.{module}")
                {
                    return new ProjectNode(folder, ProjectKind.ModuleImpl, module);
                }
                if (folder == $"Ecommerce.{module}.Contracts")
                {
                    return new ProjectNode(folder, ProjectKind.ModuleContracts, module);
                }
                return null;
            default:
                return null;
        }
    }

    public static bool IsBuildOutput(string path)
    {
        string relative = Path.GetRelativePath(FindRoot(), path);
        string[] segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin") || segments.Contains("obj");
    }

    private static string FindRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Ecommerce.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Ecommerce.slnx not found above " + AppContext.BaseDirectory);
    }
}
