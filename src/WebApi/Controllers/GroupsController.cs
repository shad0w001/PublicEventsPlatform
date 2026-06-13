using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Groups;
using Application.Groups.ChangeMemberRole;
using Application.Groups.CreateGroup;
using Application.Groups.GetGroup;
using Application.Groups.ListGroupMembers;
using Application.Groups.RemoveGroupMember;
using Application.Groups.TransferOwnership;
using Application.Groups.UpdateGroup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/groups")]
public sealed class GroupsController(
    ICommandHandler<CreateGroupCommand, GroupResponse> createGroupHandler,
    IQueryHandler<GetGroupQuery, PublicGroupResponse> getGroupHandler,
    ICommandHandler<UpdateGroupCommand, GroupResponse> updateGroupHandler,
    IQueryHandler<ListGroupMembersQuery, IReadOnlyList<GroupMemberResponse>> listGroupMembersHandler,
    ICommandHandler<ChangeMemberRoleCommand> changeMemberRoleHandler,
    ICommandHandler<RemoveGroupMemberCommand> removeGroupMemberHandler,
    ICommandHandler<TransferOwnershipCommand> transferOwnershipHandler) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateGroupCommand command,
        CancellationToken cancellationToken)
    {
        var result = await createGroupHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{groupId:guid}")]
    public async Task<IActionResult> Get(Guid groupId, CancellationToken cancellationToken)
    {
        var result = await getGroupHandler.Handle(new GetGroupQuery(groupId), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpPatch("{groupId:guid}")]
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
    [HttpGet("{groupId:guid}/members")]
    public async Task<IActionResult> ListMembers(Guid groupId, CancellationToken cancellationToken)
    {
        var result = await listGroupMembersHandler.Handle(new ListGroupMembersQuery(groupId), cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpPatch("{groupId:guid}/members/{userId:guid}")]
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
