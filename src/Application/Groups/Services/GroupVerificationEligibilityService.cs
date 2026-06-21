using Application.Abstractions.Data;
using Domain.Events;
using Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace Application.Groups.Services;

internal sealed class GroupVerificationEligibilityService(IApplicationDbContext context)
{
    public async Task<GroupVerificationEligibilitySnapshot> GetSnapshotAsync(
        Guid groupId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var verifiedMemberCount = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .Join(
                context.Users.Where(u => u.EmailVerified),
                membership => membership.UserId,
                user => user.Id,
                (_, _) => 1)
            .CountAsync(cancellationToken);

        var completedEventCount = await context.Events
            .AsNoTracking()
            .Where(e =>
                e.Status == EventStatus.Published &&
                e.DeletedAt == null &&
                e.EndTime < utcNow &&
                e.Organizers.Any(o => o.ParticipantId == groupId))
            .CountAsync(cancellationToken);

        var meetsEligibility =
            verifiedMemberCount >= GroupVerificationConstants.MinVerifiedMembers &&
            completedEventCount >= GroupVerificationConstants.MinCompletedEvents;

        return new GroupVerificationEligibilitySnapshot(
            verifiedMemberCount,
            completedEventCount,
            GroupVerificationConstants.MinVerifiedMembers,
            GroupVerificationConstants.MinCompletedEvents,
            meetsEligibility);
    }
}
