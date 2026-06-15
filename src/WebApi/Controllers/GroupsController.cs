using Application.Abstractions.Authentication;
using Application.Abstractions.Media;
using Application.Abstractions.Messaging;
using Application.Groups;
using Application.Groups.CancelGroupJoinApplication;
using Application.Groups.ChangeMemberRole;
using Application.Groups.CreateGroup;
using Application.Groups.DecideGroupJoinApplication;
using Application.Groups.DeleteGroup;
using Application.Groups.GetGroup;
using Application.Groups.JoinGroup;
using Application.Groups.ListGroupJoinApplications;
using Application.Groups.ListGroupMembers;
using Application.Groups.ListMyGroups;
using Application.Groups.RemoveGroupMember;
using Application.Groups.SubmitGroupJoinApplication;
using Application.Groups.TransferOwnership;
using Application.Groups.UpdateGroup;
using Application.Groups.UploadGroupProfileImage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using SharedKernel;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/groups")]
[SwaggerTag("Groups")]
public sealed class GroupsController(
    ICommandHandler<CreateGroupCommand, GroupResponse> createGroupHandler,
    IQueryHandler<GetGroupQuery, PublicGroupResponse> getGroupHandler,
    IQueryHandler<ListMyGroupsQuery, IReadOnlyList<MyGroupMembershipResponse>> listMyGroupsHandler,
    ICommandHandler<UpdateGroupCommand, GroupResponse> updateGroupHandler,
    ICommandHandler<DeleteGroupCommand> deleteGroupHandler,
    IQueryHandler<ListGroupMembersQuery, IReadOnlyList<GroupMemberResponse>> listGroupMembersHandler,
    ICommandHandler<ChangeMemberRoleCommand> changeMemberRoleHandler,
    ICommandHandler<RemoveGroupMemberCommand> removeGroupMemberHandler,
    ICommandHandler<TransferOwnershipCommand> transferOwnershipHandler,
    ICommandHandler<JoinGroupCommand, GroupJoinMembershipResponse> joinGroupHandler,
    ICommandHandler<SubmitGroupJoinApplicationCommand, GroupJoinApplicationResponse> submitApplicationHandler,
    IQueryHandler<ListGroupJoinApplicationsQuery, IReadOnlyList<GroupJoinApplicationResponse>> listApplicationsHandler,
    ICommandHandler<DecideGroupJoinApplicationCommand> decideApplicationHandler,
    ICommandHandler<CancelGroupJoinApplicationCommand> cancelApplicationHandler,
    ICommandHandler<UploadGroupProfileImageCommand, GroupResponse> uploadGroupProfileImageHandler) : ControllerBase
{
    [Authorize]
    [HttpPost]
    [SwaggerOperation(
        Summary = "Create a group",
        Description = """
            Creates a new organization. Requires verified email.
            The caller becomes Owner. Returns 201 with the created group and a Location header to GET the group page.
            """)]
    [SwaggerResponse(StatusCodes.Status201Created, "Group created", typeof(GroupResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Validation failed (e.g. invalid name)", typeof(ProblemDetails))]
    public async Task<IActionResult> Create(
        [FromBody] CreateGroupCommand command,
        CancellationToken cancellationToken)
    {
        var result = await createGroupHandler.Handle(command, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return result.ToCreatedResult(nameof(Get), new { groupId = result.Value.Id });
    }

    [Authorize]
    [HttpGet("mine")]
    [SwaggerOperation(
        Summary = "List my group memberships",
        Description = """
            Returns all active groups the verified caller belongs to, with role and join date.
            Soft-deleted organizations are excluded. Sorted by role rank (Owner first), then most recently joined.
            Returns 200 with an empty list when the user has no memberships.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Memberships", typeof(IReadOnlyList<MyGroupMembershipResponse>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken)
    {
        var result = await listMyGroupsHandler.Handle(new ListMyGroupsQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{groupId:guid}")]
    [SwaggerOperation(
        Summary = "Get group public page",
        Description = """
            Returns public group fields and member count. Anonymous access allowed.
            Authenticated members receive an additional `myRole` field; non-members and anonymous callers do not.
            Deleted groups return 404.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Group page", typeof(PublicGroupResponse))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group not found or deleted", typeof(ProblemDetails))]
    public async Task<IActionResult> Get(Guid groupId, CancellationToken cancellationToken)
    {
        var result = await getGroupHandler.Handle(new GetGroupQuery(groupId), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpPatch("{groupId:guid}")]
    [SwaggerOperation(
        Summary = "Update group profile",
        Description = """
            Partial update of name, description, profile image URL, and join policy.
            Requires verified email and Administrator or Owner role.
            At least one field must be provided. Returns 200 with the updated group.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Group updated", typeof(GroupResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient role", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group not found or deleted", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "No fields to update or validation failed", typeof(ProblemDetails))]
    public async Task<IActionResult> Update(
        Guid groupId,
        [FromBody] UpdateGroupRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateGroupCommand(
            groupId,
            request.Name,
            request.Description,
            request.ProfileImageUrl,
            request.JoinPolicy);

        var result = await updateGroupHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpPost("{groupId:guid}/profile-image")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Upload group profile image",
        Description = """
            Uploads a profile image for the organization. Requires verified email and Administrator or Owner role.
            Accepts JPEG, PNG, or WebP up to profile limits; stored as WebP under /uploads/groups/.
            Alternative to setting profileImageUrl via PATCH. Returns 200 with the updated group.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Profile image uploaded", typeof(GroupResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid or missing file", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient role", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group not found or deleted", typeof(ProblemDetails))]
    public async Task<IActionResult> UploadProfileImage(
        Guid groupId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Result.Failure<GroupResponse>(MediaErrors.EmptyFile).ToActionResult();
        }

        await using var stream = file.OpenReadStream();
        var upload = new MediaUploadRequest(stream, file.ContentType, file.Length, file.FileName);
        var result = await uploadGroupProfileImageHandler.Handle(
            new UploadGroupProfileImageCommand(groupId, upload),
            cancellationToken);

        return result.ToActionResult();
    }

    [Authorize]
    [HttpDelete("{groupId:guid}")]
    [SwaggerOperation(
        Summary = "Soft-delete a group",
        Description = """
            Owner-only. Sets DeletedAt and clears pending join applications; memberships are retained for potential restore.
            Returns 204 on success. Returns 404 if the group is missing or already deleted (HTTP is not idempotent).
            Cancel future events and refund tickets are handled asynchronously in Phase 7.
            """)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Group soft-deleted")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permissions", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group not found or already deleted", typeof(ProblemDetails))]
    public async Task<IActionResult> Delete(Guid groupId, CancellationToken cancellationToken)
    {
        var result = await deleteGroupHandler.Handle(new DeleteGroupCommand(groupId), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpPost("{groupId:guid}/join")]
    [SwaggerOperation(
        Summary = "Join an open group",
        Description = """
            Instantly adds the verified caller as Member when the group's join policy is Open.
            Returns 201 with membership details. Fails with 409 if already a member or the group requires applications.
            """)]
    [SwaggerResponse(StatusCodes.Status201Created, "Joined successfully", typeof(GroupJoinMembershipResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group not found or deleted", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Already a member or group is not open for join", typeof(ProblemDetails))]
    public async Task<IActionResult> Join(Guid groupId, CancellationToken cancellationToken)
    {
        var result = await joinGroupHandler.Handle(new JoinGroupCommand(groupId), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return result.ToCreatedResult(nameof(Get), new { groupId });
    }

    [Authorize]
    [HttpPost("{groupId:guid}/applications")]
    [SwaggerOperation(
        Summary = "Submit a join application",
        Description = """
            Creates a pending join application when the group's join policy is ApplicationRequired.
            Requires verified email. Returns 201 with the application.
            Fails with 409 if already a member, policy is Open, a pending application exists, or reapply cooldown is active.
            """)]
    [SwaggerResponse(StatusCodes.Status201Created, "Application submitted", typeof(GroupJoinApplicationResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group not found or deleted", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Not eligible to apply", typeof(ProblemDetails))]
    public async Task<IActionResult> SubmitApplication(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var result = await submitApplicationHandler.Handle(
            new SubmitGroupJoinApplicationCommand(groupId),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return result.ToCreatedResult(
            nameof(ListApplications),
            new { groupId, applicationId = result.Value.Id });
    }

    [Authorize]
    [HttpGet("{groupId:guid}/applications")]
    [SwaggerOperation(
        Summary = "List join applications for a group",
        Description = """
            Requires verified email. Moderator, Administrator, and Owner see the full pending queue.
            Other members and applicants see only their own applications for this group.
            Returns 200 with an empty list when there are no visible applications.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Applications", typeof(IReadOnlyList<GroupJoinApplicationResponse>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group not found or deleted", typeof(ProblemDetails))]
    public async Task<IActionResult> ListApplications(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var result = await listApplicationsHandler.Handle(
            new ListGroupJoinApplicationsQuery(groupId),
            cancellationToken);

        return result.ToActionResult();
    }

    [Authorize]
    [HttpPatch("{groupId:guid}/applications/{applicationId:guid}")]
    [SwaggerOperation(
        Summary = "Approve or reject a join application",
        Description = """
            Moderator, Administrator, or Owner decides a pending application (Approve adds Member; Reject closes the application).
            Requires verified email. Returns 204 on success.
            """)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Application decided")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient role", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group or application not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Application is not pending", typeof(ProblemDetails))]
    public async Task<IActionResult> DecideApplication(
        Guid groupId,
        Guid applicationId,
        [FromBody] DecideGroupJoinApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DecideGroupJoinApplicationCommand(
            groupId,
            applicationId,
            request.Decision);

        var result = await decideApplicationHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpDelete("{groupId:guid}/applications/{applicationId:guid}")]
    [SwaggerOperation(
        Summary = "Cancel a pending join application",
        Description = """
            Allows the applicant to withdraw their own pending application. Requires verified email.
            Returns 204 on success.
            """)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Application cancelled")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or not the applicant", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group or application not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Application is not pending", typeof(ProblemDetails))]
    public async Task<IActionResult> CancelApplication(
        Guid groupId,
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var result = await cancelApplicationHandler.Handle(
            new CancelGroupJoinApplicationCommand(groupId, applicationId),
            cancellationToken);

        return result.ToActionResult();
    }

    [Authorize]
    [HttpGet("{groupId:guid}/members")]
    [SwaggerOperation(
        Summary = "List group members",
        Description = """
            Returns all members with username, role, and joined date. Requires verified email and group membership.
            Members are ordered by role rank (Owner first), then join date.
            """)]
    [SwaggerResponse(StatusCodes.Status200OK, "Member list", typeof(IReadOnlyList<GroupMemberResponse>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or not a member", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group not found or deleted", typeof(ProblemDetails))]
    public async Task<IActionResult> ListMembers(Guid groupId, CancellationToken cancellationToken)
    {
        var result = await listGroupMembersHandler.Handle(new ListGroupMembersQuery(groupId), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpPatch("{groupId:guid}/members/{userId:guid}")]
    [SwaggerOperation(
        Summary = "Change a member's role",
        Description = """
            Moderator, Administrator, or Owner may change another member's role (not Owner—that uses transfer ownership).
            Self-demotion is blocked. Requires verified email. Returns 204 on success.
            """)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Role updated")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified, insufficient role, or self-demotion", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group or target member not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Cannot change Owner role via this endpoint", typeof(ProblemDetails))]
    public async Task<IActionResult> ChangeMemberRole(
        Guid groupId,
        Guid userId,
        [FromBody] ChangeMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ChangeMemberRoleCommand(groupId, userId, request.Role);
        var result = await changeMemberRoleHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpDelete("{groupId:guid}/members/{userId:guid}")]
    [SwaggerOperation(
        Summary = "Remove a member or leave the group",
        Description = """
            Moderator+ may remove lower-ranked members; any member may remove themselves (leave).
            Owner cannot leave without transferring ownership first. Requires verified email. Returns 204 on success.
            """)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Member removed or left")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or insufficient permission", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group or target member not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Owner cannot leave without transfer", typeof(ProblemDetails))]
    public async Task<IActionResult> RemoveMember(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await removeGroupMemberHandler.Handle(
            new RemoveGroupMemberCommand(groupId, userId),
            cancellationToken);

        return result.ToActionResult();
    }

    [Authorize]
    [HttpPost("{groupId:guid}/transfer-ownership")]
    [SwaggerOperation(
        Summary = "Transfer group ownership",
        Description = """
            Owner-only. Transfers Owner role to an existing member; the previous owner becomes Administrator.
            Requires verified email. Returns 204 on success.
            """)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Ownership transferred")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not authenticated", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Email not verified or caller is not Owner", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Group or target member not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid transfer target", typeof(ProblemDetails))]
    public async Task<IActionResult> TransferOwnership(
        Guid groupId,
        [FromBody] TransferOwnershipRequest request,
        CancellationToken cancellationToken)
    {
        var result = await transferOwnershipHandler.Handle(
            new TransferOwnershipCommand(groupId, request.NewOwnerUserId),
            cancellationToken);

        return result.ToActionResult();
    }
}

public sealed record UpdateGroupRequest(
    string? Name = null,
    string? Description = null,
    string? ProfileImageUrl = null,
    Domain.Groups.GroupJoinPolicy? JoinPolicy = null);

public sealed record ChangeMemberRoleRequest(Domain.Groups.GroupMemberRole Role);

public sealed record TransferOwnershipRequest(Guid NewOwnerUserId);

public sealed record DecideGroupJoinApplicationRequest(
    Application.Groups.DecideGroupJoinApplication.JoinApplicationDecision Decision);
