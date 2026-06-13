using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Groups.Services;

internal sealed class GroupAccessService(IApplicationDbContext context)
{
    public async Task<Result<Group>> GetActiveGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .Include(g => g.GroupMemberships)
            .Include(g => g.JoinApplications)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

        if (group is null)
        {
            return Result.Failure<Group>(GroupErrors.NotFound(groupId));
        }

        if (group.IsDeleted)
        {
            return Result.Failure<Group>(GroupErrors.Deleted(groupId));
        }

        return group;
    }

    public Result<GroupMembership> GetMembership(Group group, Guid userId)
    {
        var membership = group.GroupMemberships.FirstOrDefault(m => m.UserId == userId);
        return membership is null
            ? Result.Failure<GroupMembership>(GroupErrors.NotMember)
            : membership;
    }
}
