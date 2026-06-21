using Application.Abstractions.Messaging;
using Application.Groups;
using Application.Groups.SubmitGroupVerificationApplication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}/verification-application")]
[SwaggerTag("GroupVerification")]
public sealed class GroupVerificationApplicationsController(
    ICommandHandler<SubmitGroupVerificationApplicationCommand, GroupVerificationApplicationResponse> submitHandler)
    : ControllerBase
{
    [HttpPost]
    [SwaggerOperation(
        Summary = "Submit a verification application",
        Description = """
            Applies for verified-organization status on behalf of the group.
            Requires verified email and Organizer+ role on the group. Empty request body.
            Eligibility: at least 5 members with verified email and 10 completed published group-hosted events.
            Returns 201 with the pending application.
            Fails with 409 if already verified, not eligible, a pending application exists, or reapply cooldown is active.
            """)]
    [SwaggerResponse(StatusCodes.Status201Created, "Application submitted", typeof(GroupVerificationApplicationResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient group role", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group not found or deleted", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Not eligible to apply", typeof(ProblemDetails))]
    public async Task<IActionResult> Submit(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var result = await submitHandler.Handle(
            new SubmitGroupVerificationApplicationCommand(groupId),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return result.ToCreatedResult(
            nameof(AdminGroupVerificationController.GetApplication),
            new { applicationId = result.Value.Id });
    }
}
