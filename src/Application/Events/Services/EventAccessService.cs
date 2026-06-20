using Application.Abstractions.Data;
using Application.Events.Services;
using Domain.Events;
using Domain.Groups;
using Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Events.Services;

public sealed record EventHostContext(Guid HostParticipantId, bool HostIsGroup);

public sealed record EventAttendeeParticipantContext(
    Guid ParticipantId,
    bool ParticipantIsGroup);

public sealed record EventEditAccess(
    Guid HostParticipantId,
    bool HostIsGroup,
    GroupMemberRole? GroupRole);

internal sealed class EventAccessService(IApplicationDbContext context)
{
    public Task<Result<EventHostContext>> ResolveHostForCreateAsync(
        Guid? hostId,
        Guid userId,
        CancellationToken cancellationToken) =>
        ResolveActingParticipantAsync(
            hostId,
            userId,
            GroupPermissions.CanCreateEventsAsGroup,
            EventErrors.InsufficientHostPermissions,
            cancellationToken);

    public async Task<Result<EventAttendeeParticipantContext>> ResolveAttendeeParticipantAsync(
        Guid? participantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await ResolveActingParticipantAsync(
            participantId,
            userId,
            GroupPermissions.CanCreateEventsAsGroup,
            EventErrors.InsufficientHostPermissions,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<EventAttendeeParticipantContext>(result.Error);
        }

        var host = result.Value;
        return new EventAttendeeParticipantContext(host.HostParticipantId, host.HostIsGroup);
    }

    public async Task<Result<EventAttendeeParticipantContext>> ResolveTicketPurchaseParticipantAsync(
        Guid? participantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await ResolveActingParticipantAsync(
            participantId,
            userId,
            GroupPermissions.CanBuyTicketsAsGroup,
            TicketErrors.InsufficientPurchasePermissions,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<EventAttendeeParticipantContext>(result.Error);
        }

        var host = result.Value;
        return new EventAttendeeParticipantContext(host.HostParticipantId, host.HostIsGroup);
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

    public async Task<Result<Event>> GetActiveEventForDoorValidationAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var @event = await context.Events
            .AsNoTracking()
            .Include(e => e.Organizers)
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

    public async Task<Result<Event>> GetActiveEventForAdmissionTypeUpdateAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var @event = await context.Events
            .Include(e => e.Organizers)
            .Include(e => e.Locations)
            .Include(e => e.Attendees)
            .Include(e => e.TicketTypes)
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

    public async Task<Result<Event>> GetActiveEventWithAttendeesAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var @event = await context.Events
            .Include(e => e.Organizers)
            .Include(e => e.Attendees)
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

    public async Task<Result<Event>> GetActiveEventWithPluginsAsync(
        Guid eventId,
        bool includePluginData,
        CancellationToken cancellationToken)
    {
        IQueryable<Event> query = context.Events
            .Include(e => e.Organizers)
            .Include(e => e.Locations);

        query = includePluginData
            ? query.Include(e => e.Plugins).ThenInclude(u => u.Data)
            : query.Include(e => e.Plugins);

        var @event = await query.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

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

    public async Task<Result<Event>> GetActiveEventWithTicketTypesAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var @event = await context.Events
            .Include(e => e.Organizers)
            .Include(e => e.Locations)
            .Include(e => e.TicketTypes)
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

    public async Task<Result<Event>> GetActiveEventForDisplayAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var @event = await context.Events
            .Include(e => e.Organizers)
            .Include(e => e.Locations)
            .Include(e => e.Attendees)
            .Include(e => e.TicketTypes)
            .Include(e => e.Plugins).ThenInclude(u => u.Data)
            .Include(e => e.Plugins).ThenInclude(u => u.Plugin)
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

    public async Task<IReadOnlyList<Guid>> GetOrganizerPlusGroupIdsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await context.GroupMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId &&
                        (m.Role == GroupMemberRole.Organizer ||
                         m.Role == GroupMemberRole.Administrator ||
                         m.Role == GroupMemberRole.Owner))
            .Select(m => m.GroupId)
            .ToListAsync(cancellationToken);

    public async Task<Result> CanViewAsBuyerAsync(
        Guid buyerParticipantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (buyerParticipantId == userId)
        {
            return Result.Success();
        }

        var organizerPlusGroupIds = await GetOrganizerPlusGroupIdsAsync(userId, cancellationToken);
        if (organizerPlusGroupIds.Contains(buyerParticipantId))
        {
            return Result.Success();
        }

        return Result.Failure(TicketErrors.InsufficientPurchasePermissions);
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

    public async Task<int> CountRecentPublishesByHostAsync(
        Guid hostParticipantId,
        CancellationToken cancellationToken)
    {
        var windowStart = DateTime.UtcNow.AddDays(-7);

        return await context.Events
            .AsNoTracking()
            .Where(e => e.DeletedAt == null
                        && e.PublishedAt != null
                        && e.PublishedAt >= windowStart
                        && e.Organizers.Any(o => o.ParticipantId == hostParticipantId))
            .CountAsync(cancellationToken);
    }

    private async Task<Result<EventHostContext>> ResolveActingParticipantAsync(
        Guid? participantId,
        Guid userId,
        Func<GroupMemberRole, bool> groupPermissionCheck,
        Error insufficientPermissionsError,
        CancellationToken cancellationToken)
    {
        var resolvedParticipantId = participantId ?? userId;

        if (resolvedParticipantId == userId)
        {
            return new EventHostContext(userId, HostIsGroup: false);
        }

        var group = await context.Groups
            .AsNoTracking()
            .Where(g => g.Id == resolvedParticipantId)
            .Select(g => new { g.Id, g.DeletedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (group is not null)
        {
            if (group.DeletedAt is not null)
            {
                return Result.Failure<EventHostContext>(EventErrors.HostNotFound(resolvedParticipantId));
            }

            var role = await context.GroupMemberships
                .AsNoTracking()
                .Where(m => m.GroupId == resolvedParticipantId && m.UserId == userId)
                .Select(m => (GroupMemberRole?)m.Role)
                .FirstOrDefaultAsync(cancellationToken);

            if (role is null || !groupPermissionCheck(role.Value))
            {
                return Result.Failure<EventHostContext>(insufficientPermissionsError);
            }

            return new EventHostContext(resolvedParticipantId, HostIsGroup: true);
        }

        var isOtherUser = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == resolvedParticipantId, cancellationToken);

        if (isOtherUser)
        {
            return Result.Failure<EventHostContext>(insufficientPermissionsError);
        }

        return Result.Failure<EventHostContext>(EventErrors.HostNotFound(resolvedParticipantId));
    }
}
