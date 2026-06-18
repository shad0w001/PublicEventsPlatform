using SharedKernel;

namespace Domain.Events.Services;

public static class EventAttendeeService
{
    public static Result<EventAttendee> SetRsvpStatus(
        Event @event,
        Guid participantId,
        Guid hostParticipantId,
        EventAttendeeStatus status,
        DateTime utcNow)
    {
        var eligibilityResult = EnsureRsvpEligible(@event);
        if (eligibilityResult.IsFailure)
        {
            return Result.Failure<EventAttendee>(eligibilityResult.Error);
        }

        if (participantId == hostParticipantId && status == EventAttendeeStatus.NotGoing)
        {
            return Result.Failure<EventAttendee>(EventAttendeeErrors.HostCannotSetNotGoing);
        }

        var attendee = FindOrCreateAttendee(@event, participantId);
        ApplyRegisteredAt(attendee, status, utcNow);
        attendee.Status = status;

        return attendee;
    }

    public static Result<EventAttendee?> EnsureHostGoing(Event @event, DateTime utcNow)
    {
        if (@event.AdmissionType != AdmissionType.Free)
        {
            return Result.Success<EventAttendee?>(null);
        }

        var hostParticipantId = @event.Organizers.Single().ParticipantId;
        var attendee = FindOrCreateAttendee(@event, hostParticipantId);
        ApplyRegisteredAt(attendee, EventAttendeeStatus.Going, utcNow);
        attendee.Status = EventAttendeeStatus.Going;

        return attendee;
    }

    public static (int Going, int Interested, int ResponseCount) CountRsvps(
        IReadOnlyList<EventAttendee> attendees)
    {
        var going = attendees.Count(a => a.Status == EventAttendeeStatus.Going);
        var interested = attendees.Count(a => a.Status == EventAttendeeStatus.Interested);

        return (going, interested, going + interested);
    }

    private static Result EnsureRsvpEligible(Event @event)
    {
        if (@event.IsDeleted)
        {
            return Result.Failure(EventErrors.Deleted(@event.Id));
        }

        if (@event.Status == EventStatus.Cancelled)
        {
            return Result.Failure(EventErrors.CannotModifyCancelled);
        }

        if (@event.Status != EventStatus.Published)
        {
            return Result.Failure(EventErrors.NotPublished);
        }

        if (@event.AdmissionType != AdmissionType.Free)
        {
            return Result.Failure(EventAttendeeErrors.PaidAdmissionNotAllowed);
        }

        return Result.Success();
    }

    private static EventAttendee FindOrCreateAttendee(Event @event, Guid participantId)
    {
        var existing = @event.Attendees.FirstOrDefault(a => a.ParticipantId == participantId);
        if (existing is not null)
        {
            return existing;
        }

        var attendee = new EventAttendee
        {
            EventId = @event.Id,
            ParticipantId = participantId,
            Event = @event
        };

        @event.Attendees.Add(attendee);
        return attendee;
    }

    private static void ApplyRegisteredAt(
        EventAttendee attendee,
        EventAttendeeStatus status,
        DateTime utcNow)
    {
        if (attendee.RegisteredAt is not null)
        {
            return;
        }

        if (status is EventAttendeeStatus.Going or EventAttendeeStatus.Interested)
        {
            attendee.RegisteredAt = utcNow;
        }
    }
}
