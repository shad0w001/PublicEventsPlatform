using Domain.Participants;

namespace Domain.Events;

public class EventAttendee
{
    public Guid EventId { get; set; }
    public Guid ParticipantId { get; set; }

    public Event Event { get; set; } = null!;
    public Participant Participant { get; set; } = null!;

    public DateTime? RegisteredAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public EventAttendeeStatus Status { get; set; }
}
