using Application.Abstractions.Messaging;
using Domain.Events;

namespace Application.Events.SetEventRsvp;

public sealed record SetEventRsvpCommand(
    Guid EventId,
    Guid? ParticipantId,
    EventAttendeeStatus Status) : ICommand<EventRsvpResponse>;
