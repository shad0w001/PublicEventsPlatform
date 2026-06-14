using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using SharedKernel;

namespace Application.Groups.SubmitGroupJoinApplication;

internal sealed class SubmitGroupJoinApplicationCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService)
    : ICommandHandler<SubmitGroupJoinApplicationCommand, GroupJoinApplicationResponse>
{
    public async Task<Result<GroupJoinApplicationResponse>> Handle(
        SubmitGroupJoinApplicationCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<GroupJoinApplicationResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<GroupJoinApplicationResponse>(userResult.Error);
        }

        var groupResult = await groupAccessService.GetActiveGroupAsync(command.GroupId, cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result.Failure<GroupJoinApplicationResponse>(groupResult.Error);
        }

        var group = groupResult.Value;
        var submitResult = GroupService.SubmitJoinApplication(
            group,
            userResult.Value.Id,
            DateTime.UtcNow);

        if (submitResult.IsFailure)
        {
            return Result.Failure<GroupJoinApplicationResponse>(submitResult.Error);
        }

        var application = submitResult.Value;
        context.GroupJoinApplications.Add(application);
        await context.SaveChangesAsync(cancellationToken);

        return GroupJoinApplicationMapper.ToResponse(application, userResult.Value.Username);
    }
}
