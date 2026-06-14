using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using SharedKernel;

namespace Application.Groups.DeleteGroup;

internal sealed class DeleteGroupCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService)
    : ICommandHandler<DeleteGroupCommand>
{
    public async Task<Result> Handle(DeleteGroupCommand command, CancellationToken cancellationToken)
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

        var membershipResult = groupAccessService.GetMembership(group, userResult.Value.Id);
        if (membershipResult.IsFailure ||
            !GroupPermissions.CanSoftDeleteGroup(membershipResult.Value.Role))
        {
            return Result.Failure(GroupErrors.InsufficientPermissions());
        }

        var deleteResult = GroupService.SoftDelete(group);
        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
