using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using SharedKernel;

namespace Application.Groups.ChangeMemberRole;

internal sealed class ChangeMemberRoleCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService)
    : ICommandHandler<ChangeMemberRoleCommand>
{
    public async Task<Result> Handle(ChangeMemberRoleCommand command, CancellationToken cancellationToken)
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

        var actorMembershipResult = groupAccessService.GetMembership(group, userResult.Value.Id);
        if (actorMembershipResult.IsFailure)
        {
            return Result.Failure(actorMembershipResult.Error);
        }

        var actorRole = actorMembershipResult.Value.Role;

        if (!GroupPermissions.CanManageMembers(actorRole))
        {
            return Result.Failure(GroupErrors.InsufficientPermissions());
        }

        if (!GroupPermissions.CanAssignRole(actorRole, command.Role))
        {
            return Result.Failure(GroupErrors.InsufficientPermissions());
        }

        var changeResult = GroupService.ChangeMemberRole(group, command.UserId, command.Role);
        if (changeResult.IsFailure)
        {
            return changeResult;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
