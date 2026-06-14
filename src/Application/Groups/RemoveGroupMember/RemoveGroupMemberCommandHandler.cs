using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using SharedKernel;

namespace Application.Groups.RemoveGroupMember;

internal sealed class RemoveGroupMemberCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService)
    : ICommandHandler<RemoveGroupMemberCommand>
{
    public async Task<Result> Handle(RemoveGroupMemberCommand command, CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return gateResult;
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure(userResult.Error);
        }

        var groupResult = await groupAccessService.GetActiveGroupAsync(command.GroupId, cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result.Failure(groupResult.Error);
        }

        var group = groupResult.Value;
        var actorUserId = userResult.Value.Id;
        var isSelfLeave = actorUserId == command.UserId;

        if (!isSelfLeave)
        {
            var actorMembershipResult = groupAccessService.GetMembership(group, actorUserId);
            if (actorMembershipResult.IsFailure)
            {
                return Result.Failure(actorMembershipResult.Error);
            }

            if (!GroupPermissions.CanManageMembers(actorMembershipResult.Value.Role))
            {
                return Result.Failure(GroupErrors.InsufficientPermissions());
            }
        }

        var removeResult = GroupService.LeaveOrRemoveMember(group, actorUserId, command.UserId);
        if (removeResult.IsFailure)
        {
            return removeResult;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
