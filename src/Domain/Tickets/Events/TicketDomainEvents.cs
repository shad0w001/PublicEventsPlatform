using SharedKernel;

namespace Domain.Tickets.Events;

public sealed record TicketPurchaseCompleted(
    Guid OrderId,
    Guid EventId,
    Guid ParticipantId,
    int TicketCount) : DomainEvent;
