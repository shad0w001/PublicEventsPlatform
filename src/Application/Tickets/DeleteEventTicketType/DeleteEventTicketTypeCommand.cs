using Application.Abstractions.Messaging;

namespace Application.Tickets.DeleteEventTicketType;

public sealed record DeleteEventTicketTypeCommand(
    Guid EventId,
    Guid TicketTypeId) : ICommand;
