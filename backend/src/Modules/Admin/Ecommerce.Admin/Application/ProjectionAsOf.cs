using Ecommerce.SharedKernel.Projections;

namespace Ecommerce.Admin.Application;

internal static class ProjectionAsOf
{
    /// <summary>The oldest projector's as-of time: the dashboard figures are as fresh as the slowest read model. With no projectors it is now.</summary>
    public static DateTimeOffset From(IReadOnlyList<ProjectorHealth> projectors, DateTimeOffset now) =>
        projectors.Count == 0 ? now : projectors.Min(p => p.AsOf);
}
