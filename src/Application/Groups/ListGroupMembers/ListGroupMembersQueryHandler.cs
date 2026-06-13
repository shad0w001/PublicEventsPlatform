using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Groups.Services;
using Application.Users.Services;
using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Groups.ListGroupMembers;

internal sealed class ListGroupMembersQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    GroupAccessService groupAccessService)
    : IQueryHandler<ListGroupMembersQuery, IReadOnlyList<GroupMemberResponse>>
{
    public async Task<Result<IReadOnlyList<GroupMemberResponse>>> Handle(
        ListGroupMembersQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GroupMemberResponse>>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GroupMemberResponse>>(userResult.Error);
        }

        var groupResult = await groupAccessService.GetActiveGroupAsync(query.GroupId, cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GroupMemberResponse>>(groupResult.Error);
        }

        var group = groupResult.Value;

        var membershipResult = groupAccessService.GetMembership(group, userResult.Value.Id);
        if (membershipResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GroupMemberResponse>>(membershipResult.Error);
        }

        var members = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.GroupId == query.GroupId)
            .Join(
                context.Users,
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new
                {
                    membership.UserId,
                    user.Username,
                    membership.Role,
                    membership.JoinedAt
                })
            .ToListAsync(cancellationToken);

        IReadOnlyList<GroupMemberResponse> response = members
            .OrderByDescending(m => GroupPermissions.GetRoleRank(m.Role))
            .ThenBy(m => m.JoinedAt)
            .Select(m => new GroupMemberResponse(m.UserId, m.Username, m.Role, m.JoinedAt))
            .ToList();

        return Result.Success(response);
    }
}
