using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using SharedKernel;

namespace Application.Groups.TransferOwnership;

internal sealed class TransferOwnershipCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService)
    : ICommandHandler<TransferOwnershipCommand>
{
    public async Task<Result> Handle(TransferOwnershipCommand command, CancellationToken cancellationToken)
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

        if (actorMembershipResult.Value.Role is not GroupMemberRole.Owner)
        {
            return Result.Failure(GroupErrors.NotOwner);
        }

        var transferResult = GroupService.TransferOwnership(
            group,
            userResult.Value.Id,
            command.NewOwnerUserId);

        if (transferResult.IsFailure)
        {
            return transferResult;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
