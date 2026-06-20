using Application.Abstractions.Data;
using Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace Application.Notifications.Services;

public sealed class NotificationRecipientService(IApplicationDbContext context)
{
    public async Task<string?> GetUserEmailAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetOrganizerPlusEmailsAsync(
        Guid groupId,
        CancellationToken cancellationToken = default) =>
        await GetGroupMemberEmailsAsync(
            groupId,
            GroupPermissions.CanCreateEventsAsGroup,
            cancellationToken);

    public async Task<IReadOnlyList<string>> GetApplicationReviewerEmailsAsync(
        Guid groupId,
        CancellationToken cancellationToken = default) =>
        await GetGroupMemberEmailsAsync(
            groupId,
            GroupPermissions.CanReviewApplications,
            cancellationToken);

    public async Task<IReadOnlyList<string>> ResolveParticipantEmailsAsync(
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var userEmail = await GetUserEmailAsync(participantId, cancellationToken);
        if (userEmail is not null)
        {
            return [userEmail];
        }

        var isGroup = await context.Groups
            .AsNoTracking()
            .AnyAsync(g => g.Id == participantId, cancellationToken);

        if (!isGroup)
        {
            return [];
        }

        return await GetOrganizerPlusEmailsAsync(participantId, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> GetGroupMemberEmailsAsync(
        Guid groupId,
        Func<GroupMemberRole, bool> roleFilter,
        CancellationToken cancellationToken)
    {
        var memberships = await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .Select(m => new { m.Role, m.UserId })
            .ToListAsync(cancellationToken);

        var userIds = memberships
            .Where(m => roleFilter(m.Role))
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
        {
            return [];
        }

        return await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => u.Email)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
