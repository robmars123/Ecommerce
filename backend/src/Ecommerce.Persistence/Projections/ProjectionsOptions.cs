using Microsoft.Extensions.Options;

namespace Ecommerce.Persistence.Projections;

/// <summary>Arch 8.8.2 and 8.8.3: the checkpoint safety window, the lag target and the worker poll.</summary>
public sealed class ProjectionsOptions
{
    public int SafetyWindowSeconds { get; set; } = 30;
    public int LagTargetSeconds { get; set; } = 60;
    public int PollIntervalMs { get; set; } = 1000;
}

internal sealed class ProjectionsOptionsValidator : IValidateOptions<ProjectionsOptions>
{
    public ValidateOptionsResult Validate(string? name, ProjectionsOptions options)
    {
        List<string> failures = [];
        if (options.SafetyWindowSeconds < 1)
        {
            failures.Add("Projections:SafetyWindowSeconds must be 1 or greater.");
        }
        if (options.LagTargetSeconds < 1)
        {
            failures.Add("Projections:LagTargetSeconds must be 1 or greater.");
        }
        if (options.PollIntervalMs < 100)
        {
            failures.Add("Projections:PollIntervalMs must be 100 or greater.");
        }
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
