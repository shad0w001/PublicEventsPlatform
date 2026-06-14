using Application.Abstractions.Data;
using Domain.Events;
using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Events.Services;

public sealed record EventHostContext(Guid HostParticipantId, bool HostIsGroup);

public sealed record EventEditAccess(
    Guid HostParticipantId,
    bool HostIsGroup,
    GroupMemberRole? GroupRole);

internal sealed class EventAccessService(IApplicationDbContext context)
{
    public async Task<Result<EventHostContext>> ResolveHostForCreateAsync(
        Guid? hostId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var resolvedHostId = hostId ?? userId;

        if (resolvedHostId == userId)
        {
            return new EventHostContext(userId, HostIsGroup: false);
        }

        var group = await context.Groups
            .AsNoTracking()
            .Where(g => g.Id == resolvedHostId)
            .Select(g => new { g.Id, g.DeletedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (group is not null)
        {
            if (group.DeletedAt is not null)
            {
                return Result.Failure<EventHostContext>(EventErrors.HostNotFound(resolvedHostId));
            }

            var role = await context.GroupMemberships
                .AsNoTracking()
                .Where(m => m.GroupId == resolvedHostId && m.UserId == userId)
                .Select(m => (GroupMemberRole?)m.Role)
                .FirstOrDefaultAsync(cancellationToken);

            if (role is null || !GroupPermissions.CanCreateEventsAsGroup(role.Value))
            {
                return Result.Failure<EventHostContext>(EventErrors.InsufficientHostPermissions);
            }

            return new EventHostContext(resolvedHostId, HostIsGroup: true);
        }

        var isOtherUser = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == resolvedHostId, cancellationToken);

        if (isOtherUser)
        {
            return Result.Failure<EventHostContext>(EventErrors.InsufficientHostPermissions);
        }

        return Result.Failure<EventHostContext>(EventErrors.HostNotFound(resolvedHostId));
    }

    public async Task<Result<Event>> GetActiveEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var @event = await context.Events
            .Include(e => e.Organizers)
            .Include(e => e.Locations)
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (@event is null)
        {
            return Result.Failure<Event>(EventErrors.NotFound(eventId));
        }

        if (@event.IsDeleted)
        {
            return Result.Failure<Event>(EventErrors.Deleted(eventId));
        }

        return @event;
    }

    public Guid GetHostParticipantId(Event @event) =>
        @event.Organizers.Single().ParticipantId;

    public async Task<Result<EventEditAccess>> ResolveEditAccessAsync(
        Event @event,
        Guid userId,
        Guid userParticipantId,
        CancellationToken cancellationToken)
    {
        var hostParticipantId = GetHostParticipantId(@event);

        var hostIsGroup = await context.Groups
            .AsNoTracking()
            .AnyAsync(g => g.Id == hostParticipantId, cancellationToken);

        GroupMemberRole? groupRole = null;
        if (hostIsGroup)
        {
            groupRole = await context.GroupMemberships
                .AsNoTracking()
                .Where(m => m.GroupId == hostParticipantId && m.UserId == userId)
                .Select(m => (GroupMemberRole?)m.Role)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!EventPermissions.CanEdit(
                @event,
                userId,
                userParticipantId,
                hostParticipantId,
                hostIsGroup,
                groupRole))
        {
            return Result.Failure<EventEditAccess>(EventErrors.InsufficientPermissions());
        }

        return new EventEditAccess(hostParticipantId, hostIsGroup, groupRole);
    }
}
