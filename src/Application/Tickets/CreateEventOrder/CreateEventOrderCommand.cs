using Application.Abstractions.Messaging;
using Application.Tickets.CreateEventOrder;

namespace Application.Tickets.CreateEventOrder;

public sealed record CreateEventOrderCommand(
    Guid EventId,
    Guid TicketTypeId,
    int Quantity,
    Guid? ParticipantId = null,
    string? SuccessUrl = null,
    string? CancelUrl = null) : ICommand<CreateEventOrderResponse>;
