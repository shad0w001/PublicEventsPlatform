using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using SharedKernel;

namespace Application.Groups.DecideGroupJoinApplication;

internal sealed class DecideGroupJoinApplicationCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService)
    : ICommandHandler<DecideGroupJoinApplicationCommand>
{
    public async Task<Result> Handle(
        DecideGroupJoinApplicationCommand command,
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

        var group = groupResult.Value;

        var membershipResult = groupAccessService.GetMembership(group, userResult.Value.Id);
        if (membershipResult.IsFailure)
        {
            return Result.Failure(GroupErrors.InsufficientPermissions());
        }

        if (!GroupPermissions.CanReviewApplications(membershipResult.Value.Role))
        {
            return Result.Failure(GroupErrors.InsufficientPermissions());
        }

        var utcNow = DateTime.UtcNow;
        var decidedByUserId = userResult.Value.Id;

        Result decideResult = command.Decision switch
        {
            JoinApplicationDecision.Approve => ToResult(GroupService.ApproveApplication(
                group,
                command.ApplicationId,
                decidedByUserId,
                utcNow)),
            JoinApplicationDecision.Reject => GroupService.RejectApplication(
                group,
                command.ApplicationId,
                decidedByUserId,
                utcNow),
            _ => Result.Failure(GroupErrors.InsufficientPermissions())
        };

        if (decideResult.IsFailure)
        {
            return decideResult;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static Result ToResult(Result<GroupMembership> result) =>
        result.IsFailure ? Result.Failure(result.Error) : Result.Success();
}
