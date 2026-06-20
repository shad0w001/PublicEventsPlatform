using Application.Abstractions.Messaging;
using Domain.Events;

namespace Application.Events.SetEventRsvp;

public sealed record EventRsvpResponse(
    Guid EventId,
    Guid ParticipantId,
    bool ParticipantIsGroup,
    EventAttendeeStatus Status,
    DateTime? RegisteredAt);
