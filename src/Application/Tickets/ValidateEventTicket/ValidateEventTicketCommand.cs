using Application.Abstractions.Messaging;
using Domain.Tickets;

namespace Application.Tickets.ValidateEventTicket;

public sealed record ValidateEventTicketCommand(
    Guid EventId,
    string Code,
    TicketValidationMethod Method) : ICommand<ValidateEventTicketResponse>;
