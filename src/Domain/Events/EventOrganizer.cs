using Domain.Participants;

namespace Domain.Events;

public class EventOrganizer
{
    public Guid EventId { get; set; }
    public Guid ParticipantId { get; set; }

    public Event Event { get; set; } = null!;
    public Participant Participant { get; set; } = null!;

    public static EventOrganizer Create(Guid eventId, Guid hostParticipantId) =>
        new()
        {
            EventId = eventId,
            ParticipantId = hostParticipantId
        };
}
