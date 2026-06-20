using Domain.Events;
using Domain.Events.Services;

namespace Application.Events;

internal static class EventRsvpMapping
{
    public static EventRsvpSummaryResponse? BuildSummary(
        Event @event,
        Guid? userId,
        IReadOnlySet<Guid> organizerPlusGroupIds)
    {
        if (@event.AdmissionType != AdmissionType.Free || @event.Status == EventStatus.Draft)
        {
            return null;
        }

        var (going, interested, responseCount) = EventAttendeeService.CountRsvps(@event.Attendees);

        IReadOnlyList<EventRsvpMyStatusResponse>? myStatuses = null;
        if (userId is not null)
        {
            myStatuses = @event.Attendees
                .Where(a => a.Status is not null)
                .Where(a => a.ParticipantId == userId.Value || organizerPlusGroupIds.Contains(a.ParticipantId))
                .Select(a => new EventRsvpMyStatusResponse(
                    a.ParticipantId,
                    organizerPlusGroupIds.Contains(a.ParticipantId),
                    a.Status!.Value))
                .ToList();
        }

        return new EventRsvpSummaryResponse(going, interested, responseCount, myStatuses);
    }
}
