using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups.Services;
using SharedKernel;

namespace Application.Groups.JoinGroup;

internal sealed class JoinGroupCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService)
    : ICommandHandler<JoinGroupCommand, GroupJoinMembershipResponse>
{
    public async Task<Result<GroupJoinMembershipResponse>> Handle(
        JoinGroupCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<GroupJoinMembershipResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<GroupJoinMembershipResponse>(userResult.Error);
        }

        var groupResult = await groupAccessService.GetActiveGroupAsync(command.GroupId, cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result.Failure<GroupJoinMembershipResponse>(groupResult.Error);
        }

        var group = groupResult.Value;
        var userId = userResult.Value.Id;

        var joinResult = GroupService.JoinOpen(group, userId);
        if (joinResult.IsFailure)
        {
            return Result.Failure<GroupJoinMembershipResponse>(joinResult.Error);
        }

        var membership = joinResult.Value;
        await context.SaveChangesAsync(cancellationToken);

        return new GroupJoinMembershipResponse(membership.UserId, membership.Role, membership.JoinedAt);
    }
}
