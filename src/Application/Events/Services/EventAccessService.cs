using Application.Abstractions.Data;
using Domain.Events;
using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Events.Services;

public sealed record EventEditAccess(
    Guid HostParticipantId,
    bool HostIsGroup,
    GroupMemberRole? GroupRole);

internal sealed class EventAccessService(IApplicationDbContext context)
{
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
