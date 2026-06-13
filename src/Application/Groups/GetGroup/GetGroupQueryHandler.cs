using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Groups.GetGroup;

internal sealed class GetGroupQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetGroupQuery, PublicGroupResponse>
{
    public async Task<Result<PublicGroupResponse>> Handle(
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
            return Result.Failure<PublicGroupResponse>(GroupErrors.NotFound(query.GroupId));
        }

        if (group.DeletedAt is not null)
        {
            return Result.Failure<PublicGroupResponse>(GroupErrors.Deleted(query.GroupId));
        }

        return new PublicGroupResponse(
            group.Id,
            group.Name,
            group.Description,
            group.JoinPolicy,
            group.ProfileImageUrl,
            group.CreatedAt,
            group.MemberCount);
    }
}
