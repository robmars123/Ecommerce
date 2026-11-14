using Ecommerce.Identity.Contracts;
using Ecommerce.SharedKernel.Projections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Admin.Api;

/// <summary>Admin only (Slice 6b D5). Replay rebuilds one projector from the retained outbox; 409 while another worker holds it.</summary>
internal static class AdminProjectionsEndpoints
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("api/v1/admin/projections/{name}/replay", Replay).RequireAuthorization(AuthorizationPolicies.Admin);

    private static async Task<Results<Accepted, ProblemHttpResult>> Replay(string name, [FromServices] IProjectionReplayer replayer, CancellationToken ct) =>
        await replayer.ReplayAsync(name, ct) switch
        {
            ReplayOutcome.Done => TypedResults.Accepted((string?)null),
            ReplayOutcome.Busy => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Projection busy", detail: "projection-busy: another worker is running this projector."),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Projection not found"),
        };
}
