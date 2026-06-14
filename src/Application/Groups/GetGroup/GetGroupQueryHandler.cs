using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Services;
using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Groups.GetGroup;

internal sealed class GetGroupQueryHandler(
    IApplicationDbContext context,
    IUserIdentityAccessor identityAccessor,
    ICurrentUserService currentUserService)
    : IQueryHandler<GetGroupQuery, GroupPageResponse>
{
    public async Task<Result<GroupPageResponse>> Handle(
        GetGroupQuery query,
        CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .AsNoTracking()
            .Where(g => g.Id == query.GroupId)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Description,
                g.JoinPolicy,
                g.ProfileImageUrl,
                g.CreatedAt,
                g.DeletedAt,
                MemberCount = g.GroupMemberships.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (group is null)
        {
            return Result.Failure<GroupPageResponse>(GroupErrors.NotFound(query.GroupId));
        }

        if (group.DeletedAt is not null)
        {
            return Result.Failure<GroupPageResponse>(GroupErrors.Deleted(query.GroupId));
        }

        GroupMemberRole? myRole = null;

        if (identityAccessor.IsAuthenticated)
        {
            var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
            if (userResult.IsSuccess)
            {
                myRole = await context.GroupMemberships
                    .AsNoTracking()
                    .Where(m => m.GroupId == query.GroupId && m.UserId == userResult.Value.Id)
                    .Select(m => (GroupMemberRole?)m.Role)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        return new GroupPageResponse(
            group.Id,
            group.Name,
            group.Description,
            group.JoinPolicy,
            group.ProfileImageUrl,
            group.CreatedAt,
            group.MemberCount,
            myRole);
    }
}
