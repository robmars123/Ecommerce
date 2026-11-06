namespace Ecommerce.ArchitectureTests;

public class ClockRuleTests
{
    private static readonly string[] Banned =
    [
        "DateTime.UtcNow", "DateTime.Now", "DateTime.Today",
        "DateTimeOffset.UtcNow", "DateTimeOffset.Now", "TimeProvider.System"
    ];

    [Fact]
    public void No_source_outside_the_host_reads_the_system_clock()
    {
        string[] roots = new[]
        {
            Path.Combine(SolutionLayout.SrcDir, "Ecommerce.SharedKernel"),
            Path.Combine(SolutionLayout.SrcDir, "Ecommerce.Persistence"),
            Path.Combine(SolutionLayout.SrcDir, "Modules"),
        };
        List<string> files = roots.SelectMany(SolutionLayout.SourceFiles).ToList();

        Assert.NotEmpty(files);
        string catalog = SolutionLayout.Impl("Catalog").Directory;
        Assert.Contains(files, f => f.StartsWith(Path.Combine(catalog, "Domain"), StringComparison.Ordinal));
        Assert.Contains(files, f => f.StartsWith(Path.Combine(catalog, "Application"), StringComparison.Ordinal));
        string identity = SolutionLayout.Impl("Identity").Directory;
        Assert.Contains(files, f => f.StartsWith(Path.Combine(identity, "Domain"), StringComparison.Ordinal));
        Assert.Contains(files, f => f.StartsWith(Path.Combine(identity, "Application"), StringComparison.Ordinal));
        Assert.Contains(files, f => f.Contains("Migrations", StringComparison.Ordinal));

        List<string> hits = new List<string>();
        foreach (string? file in files)
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (string token in Banned)
                {
                    if (lines[i].Contains(token, StringComparison.Ordinal))
                    {
                        hits.Add($"{Path.GetRelativePath(SolutionLayout.Root, file)}:{i + 1} uses {token}");
                    }
                }
            }
        }

        Assert.True(hits.Count == 0, string.Join(Environment.NewLine, hits));
    }
}
