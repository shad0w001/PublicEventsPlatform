using Application.Abstractions.Data;
using Application.Abstractions.Groups;
using Domain.Events;
using Domain.Events.Services;
using Domain.Groups.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Groups.ProcessGroupSoftDeleted;

internal sealed class GroupSoftDeletedCascadeHandler(
    IApplicationDbContext context,
    TimeProvider timeProvider,
    ILogger<GroupSoftDeletedCascadeHandler> logger) : IGroupSoftDeletedCascadeHandler
{
    public async Task HandleAsync(
        GroupSoftDeleted domainEvent,
        CancellationToken cancellationToken = default)
    {
        var groupExists = await context.Groups
            .AsNoTracking()
            .AnyAsync(
                g => g.Id == domainEvent.GroupId && g.DeletedAt != null,
                cancellationToken);

        if (!groupExists)
        {
            logger.LogWarning(
                "Skipping group soft-deleted cascade for group {GroupId}: group not found or not deleted",
                domainEvent.GroupId);
            return;
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var eventsToCancel = await context.Events
            .Where(e =>
                e.DeletedAt == null &&
                e.Status == EventStatus.Published &&
                e.StartTime > utcNow &&
                e.Organizers.Any(o => o.ParticipantId == domainEvent.GroupId))
            .ToListAsync(cancellationToken);

        if (eventsToCancel.Count == 0)
        {
            logger.LogInformation(
                "No future published events to cancel for soft-deleted group {GroupId}",
                domainEvent.GroupId);
            return;
        }

        foreach (var @event in eventsToCancel)
        {
            var cancelResult = EventService.Cancel(@event);
            if (cancelResult.IsFailure)
            {
                logger.LogWarning(
                    "Failed to cancel event {EventId} during group {GroupId} soft-delete cascade: {ErrorCode}",
                    @event.Id,
                    domainEvent.GroupId,
                    cancelResult.Error.Code);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Cancelled {CancelledCount} future published event(s) for soft-deleted group {GroupId}",
            eventsToCancel.Count,
            domainEvent.GroupId);
    }
}
