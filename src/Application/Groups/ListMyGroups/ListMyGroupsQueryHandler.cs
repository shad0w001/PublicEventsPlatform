using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Services;
using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Groups.ListMyGroups;

internal sealed class ListMyGroupsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor)
    : IQueryHandler<ListMyGroupsQuery, IReadOnlyList<MyGroupMembershipResponse>>
{
    public async Task<Result<IReadOnlyList<MyGroupMembershipResponse>>> Handle(
        ListMyGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MyGroupMembershipResponse>>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MyGroupMembershipResponse>>(userResult.Error);
        }

        var memberships = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userResult.Value.Id)
            .Join(
                context.Groups.Where(g => g.DeletedAt == null),
                membership => membership.GroupId,
                group => group.Id,
                (membership, group) => new MyGroupMembershipResponse(
                    group.Id,
                    group.Name,
                    group.ProfileImageUrl,
                    group.JoinPolicy,
                    group.GroupMemberships.Count,
                    group.IsVerified,
                    membership.Role,
                    membership.JoinedAt))
            .ToListAsync(cancellationToken);

        IReadOnlyList<MyGroupMembershipResponse> response = memberships
            .OrderByDescending(m => GroupPermissions.GetRoleRank(m.MyRole))
            .ThenByDescending(m => m.JoinedAt)
            .ToList();

        return Result.Success(response);
    }
}
