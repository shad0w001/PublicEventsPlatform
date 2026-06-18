using SharedKernel;

namespace Domain.Events.Events;

public sealed record EventCreated(Guid EventId, Guid HostParticipantId, EventTier Tier) : DomainEvent;

public sealed record EventUpdated(Guid EventId) : DomainEvent;

public sealed record EventPublished(Guid EventId, Guid HostParticipantId) : DomainEvent;

public sealed record EventCancelled(Guid EventId) : DomainEvent;

public sealed record EventSoftDeleted(Guid EventId) : DomainEvent;

public sealed record EventRsvpStatusChanged(
    Guid EventId,
    Guid ParticipantId,
    EventAttendeeStatus Status,
    EventAttendeeStatus? PreviousStatus) : DomainEvent;
