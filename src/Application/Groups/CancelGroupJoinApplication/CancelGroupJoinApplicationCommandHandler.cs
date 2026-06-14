using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups.Services;
using SharedKernel;

namespace Application.Groups.CancelGroupJoinApplication;

internal sealed class CancelGroupJoinApplicationCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService)
    : ICommandHandler<CancelGroupJoinApplicationCommand>
{
    public async Task<Result> Handle(
        CancelGroupJoinApplicationCommand command,
        CancellationToken cancellationToken)
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

        var cancelResult = GroupService.CancelApplication(
            groupResult.Value,
            command.ApplicationId,
            userResult.Value.Id);

        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
