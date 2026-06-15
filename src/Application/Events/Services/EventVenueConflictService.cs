using Application.Abstractions.Data;
using Domain.Events;
using Domain.Events.EventLocations;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Events.Services;

internal sealed class EventVenueConflictService(IApplicationDbContext context)
{
    public async Task<Result> EnsureNoConflictAsync(Event @event, CancellationToken cancellationToken)
    {
        if (!@event.Locations.Any(l => l.Kind == EventLocationKind.Physical))
        {
            return Result.Success();
        }

        var candidates = await context.Events
            .AsNoTracking()
            .Include(e => e.Locations)
            .Where(e => e.Id != @event.Id
                        && e.DeletedAt == null
                        && e.Status == EventStatus.Published
                        && e.StartTime < @event.EndTime
                        && e.EndTime > @event.StartTime)
            .ToListAsync(cancellationToken);

        var occupants = candidates
            .Select(e => new EventVenueRules.PublishedEventOccupancy(
                e.Id,
                e.StartTime,
                e.EndTime,
                e.Locations.ToList()))
            .ToList();

        if (EventVenueRules.HasVenueConflict(@event, occupants))
        {
            return Result.Failure(EventErrors.VenueConflict);
        }

        return Result.Success();
    }
}
