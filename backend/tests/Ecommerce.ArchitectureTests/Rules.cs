using System.Reflection;
using System.Text.RegularExpressions;

namespace Ecommerce.ArchitectureTests;

/// <summary>Project-to-project reference rules (architecture 6.2). Pure: the same table is applied to csproj and to IL.</summary>
internal static class ReferenceRules
{
    public static List<string> Violations(ProjectNode from, IEnumerable<ProjectNode> to)
    {
        var violations = new List<string>();
        foreach (var target in to)
        {
            switch (from.Kind)
            {
                case ProjectKind.SharedKernel:
                    violations.Add($"{from.Name} (SharedKernel) must reference no project, but references {target.Name}");
                    break;
                case ProjectKind.Persistence or ProjectKind.ModuleContracts:
                    if (target.Kind != ProjectKind.SharedKernel)
                    {
                        violations.Add($"{from.Name} ({from.Kind}) may reference only Ecommerce.SharedKernel, but references {target.Name} ({target.Kind})");
                    }
                    break;
                case ProjectKind.ModuleImpl:
                    if (target.Kind is not (ProjectKind.SharedKernel or ProjectKind.Persistence or ProjectKind.ModuleContracts))
                    {
                        violations.Add($"{from.Name} (module implementation) may reference only SharedKernel, Persistence and *.Contracts, but references {target.Name} ({target.Kind})");
                    }
                    break;
                case ProjectKind.Host:
                    break;
            }
        }
        return violations;
    }
}

/// <summary>Layer rules inside a module assembly: folders are layers, namespaces mirror folders.</summary>
internal static class LayerRules
{
    public static bool InNamespace(string? ns, string prefix) =>
        ns is not null && (ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal));

    public static bool IsFrameworkAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? "";
        return name is "netstandard" or "mscorlib" or "System" || name.StartsWith("System.", StringComparison.Ordinal);
    }

    /// <summary>layer is Domain, Application or Api. owner is only used in the message.</summary>
    public static List<string> Violations(string module, string layer, Assembly ownAssembly, string owner, IEnumerable<Type> dependencies)
    {
        var violations = new List<string>();
        foreach (var dep in dependencies)
        {
            if (!IsAllowed(module, layer, ownAssembly, dep))
            {
                violations.Add($"{owner} ({module} {layer}) depends on {dep.FullName} [{dep.Assembly.GetName().Name}]");
            }
        }
        return violations;
    }

    private static bool IsAllowed(string module, string layer, Assembly own, Type dep)
    {
        var asm = dep.Assembly.GetName().Name ?? "";
        var sameAssembly = dep.Assembly == own;
        var ns = dep.Namespace;
        var domain = $"Ecommerce.{module}.Domain";
        var application = $"Ecommerce.{module}.Application";
        var infrastructure = $"Ecommerce.{module}.Infrastructure";

        switch (layer)
        {
            case "Domain":
                return IsFrameworkAssembly(dep.Assembly)
                       || asm == "Ecommerce.SharedKernel"
                       || (sameAssembly && InNamespace(ns, domain));
            case "Application":
                return IsFrameworkAssembly(dep.Assembly)
                       || asm == "Ecommerce.SharedKernel"
                       || (asm.StartsWith("Ecommerce.", StringComparison.Ordinal) && asm.EndsWith(".Contracts", StringComparison.Ordinal))
                       || (sameAssembly && (InNamespace(ns, domain) || InNamespace(ns, application)));
            case "Api":
                return !(sameAssembly && InNamespace(ns, infrastructure))
                       && asm != "Ecommerce.Persistence"
                       && !asm.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                       && !asm.StartsWith("Npgsql", StringComparison.Ordinal);
            default:
                throw new ArgumentException("Unknown layer " + layer, nameof(layer));
        }
    }
}

/// <summary>Every type a type's signature (and shape) mentions: base, interfaces, fields, properties, methods, constructors, events.</summary>
internal static class TypeDependencies
{
    private const BindingFlags All =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static IReadOnlyCollection<Type> Of(Type type)
    {
        var found = new HashSet<Type>();

        void Add(Type? t)
        {
            if (t is null || t.IsGenericParameter || t.IsGenericMethodParameter)
            {
                return;
            }
            if (t.HasElementType)
            {
                Add(t.GetElementType());
                return;
            }
            if (t.IsGenericType)
            {
                found.Add(t.GetGenericTypeDefinition());
                foreach (var arg in t.GetGenericArguments())
                {
                    Add(arg);
                }
                return;
            }
            found.Add(t);
        }

        void AddParameters(MethodBase m)
        {
            foreach (var p in m.GetParameters())
            {
                Add(p.ParameterType);
            }
        }

        Add(type.BaseType);
        foreach (var i in type.GetInterfaces())
        {
            Add(i);
        }
        foreach (var f in type.GetFields(All))
        {
            Add(f.FieldType);
        }
        foreach (var p in type.GetProperties(All))
        {
            Add(p.PropertyType);
            foreach (var ip in p.GetIndexParameters())
            {
                Add(ip.ParameterType);
            }
        }
        foreach (var m in type.GetMethods(All))
        {
            Add(m.ReturnType);
            AddParameters(m);
        }
        foreach (var c in type.GetConstructors(All))
        {
            AddParameters(c);
        }
        foreach (var e in type.GetEvents(All))
        {
            Add(e.EventHandlerType);
        }

        found.Remove(type);
        return found;
    }
}

/// <summary>Source-text rules. Reflection cannot see method bodies, so folders are also scanned as text.</summary>
internal static partial class SourceRules
{
    public static readonly string[] ForbiddenFrameworkNames =
    [
        "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Extensions",
        "Npgsql", "Serilog", "Stripe", "StackExchange.Redis",
    ];

    [GeneratedRegex(@"\bEcommerce(\.[A-Za-z_][A-Za-z0-9_]*)+")]
    private static partial Regex EcommerceName();

    /// <summary>layer is Domain, Application or Api. knownModules are all module names in the solution.</summary>
    public static List<string> Violations(string module, string layer, string file, IReadOnlyList<string> lines, IReadOnlyCollection<string> knownModules)
    {
        var violations = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var where = $"{file}:{i + 1}";

            var frameworkNames = layer switch
            {
                "Api" => ["Microsoft.EntityFrameworkCore", "Npgsql"],
                _ => ForbiddenFrameworkNames,
            };
            foreach (var name in frameworkNames)
            {
                if (line.Contains(name, StringComparison.Ordinal))
                {
                    violations.Add($"{where} names {name} in the {module} {layer} layer");
                }
            }

            foreach (Match match in EcommerceName().Matches(line))
            {
                var problem = CheckEcommerceName(module, layer, match.Value, knownModules);
                if (problem is not null)
                {
                    violations.Add($"{where} {problem}");
                }
            }
        }
        return violations;
    }

    private static string? CheckEcommerceName(string module, string layer, string name, IReadOnlyCollection<string> knownModules)
    {
        var segments = name.Split('.');
        var second = segments[1];
        var third = segments.Length > 2 ? segments[2] : null;

        if (second == "SharedKernel")
        {
            return null;
        }
        if (knownModules.Contains(second))
        {
            if (second != module)
            {
                if (third != "Contracts")
                {
                    return $"names {name}: only another module's .Contracts may be used";
                }
                // Domain uses no Contracts at all, own or another module's (architecture 6.2).
                return layer == "Domain" ? $"names {name}: the Domain layer may not use any .Contracts" : null;
            }
            var allowed = layer switch
            {
                "Domain" => new[] { "Domain" },
                "Application" => ["Domain", "Application", "Contracts"],
                _ => ["Domain", "Application", "Contracts", "Api"],
            };
            // The module's own root namespace (e.g. a namespace declaration) is never a violation by itself.
            return third is null || allowed.Contains(third) ? null : $"names {name}, not allowed in the {layer} layer";
        }
        return $"names {name}, which a {layer} layer must not reference";
    }
}
