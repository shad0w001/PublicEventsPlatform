using Application.Abstractions.Messaging;
using Application.Groups;
using Application.Groups.DecideGroupVerificationApplication;
using Application.Groups.GetGroupVerificationApplication;
using Application.Groups.ListGroupVerificationApplications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/groups/verification/applications")]
[SwaggerTag("Admin")]
public sealed class AdminGroupVerificationController(
    IQueryHandler<ListGroupVerificationApplicationsQuery, IReadOnlyList<GroupVerificationApplicationResponse>> listHandler,
    IQueryHandler<GetGroupVerificationApplicationQuery, GroupVerificationApplicationDetailResponse> getHandler,
    ICommandHandler<DecideGroupVerificationApplicationCommand> decideHandler)
    : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "List verification applications",
        Description = """
            Returns pending and historical verification applications for all organizations, newest first.
            Requires platform administrator role (Auth0 admin).
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Applications", typeof(IReadOnlyList<GroupVerificationApplicationResponse>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Not a platform administrator", typeof(ProblemDetails))]
    public async Task<IActionResult> ListApplications(CancellationToken cancellationToken)
    {
        var result = await listHandler.Handle(
            new ListGroupVerificationApplicationsQuery(),
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{applicationId:guid}")]
    [SwaggerOperation(
        Summary = "Get verification application detail",
        Description = """
            Returns application status plus current group stats and eligibility snapshot for admin review.
            Requires platform administrator role.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Application detail", typeof(GroupVerificationApplicationDetailResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Not a platform administrator", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Application or group not found", typeof(ProblemDetails))]
    public async Task<IActionResult> GetApplication(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var result = await getHandler.Handle(
            new GetGroupVerificationApplicationQuery(applicationId),
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpPatch("{applicationId:guid}")]
    [SwaggerOperation(
        Summary = "Approve or reject a verification application",
        Description = """
            Approve sets the organization verified badge (no revoke in v1). Reject keeps the org unverified.
            Requires platform administrator role. Returns 204 on success.
            """)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Decision recorded")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Not a platform administrator", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Application not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Application not pending or org already verified", typeof(ProblemDetails))]
    public async Task<IActionResult> DecideApplication(
        Guid applicationId,
        [FromBody] DecideGroupVerificationApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await decideHandler.Handle(
            new DecideGroupVerificationApplicationCommand(applicationId, request.Decision),
            cancellationToken);

        return result.ToActionResult();
    }
}

public sealed record DecideGroupVerificationApplicationRequest(VerificationApplicationDecision Decision);
