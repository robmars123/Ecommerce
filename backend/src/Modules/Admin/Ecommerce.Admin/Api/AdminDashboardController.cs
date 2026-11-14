using Ecommerce.Identity.Contracts;
using Ecommerce.SharedKernel.Projections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Admin.Api;

/// <summary>Admin only (Slice 6b D5). Replay rebuilds one projector from the retained outbox; 409 while another worker holds it.</summary>
[ApiController]
[Route("api/v1/admin/projections")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
internal sealed class AdminProjectionsController(IProjectionReplayer replayer) : ControllerBase
{
    [HttpPost("{name}/replay")]
    public async Task<IActionResult> Replay(string name, CancellationToken ct) =>
        await replayer.ReplayAsync(name, ct) switch
        {
            ReplayOutcome.Done => Accepted(),
            ReplayOutcome.Busy => Problem(statusCode: StatusCodes.Status409Conflict, title: "Projection busy", detail: "projection-busy: another worker is running this projector."),
            _ => Problem(statusCode: StatusCodes.Status404NotFound, title: "Projection not found"),
        };
}
