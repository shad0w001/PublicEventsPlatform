using Application.Abstractions.Messaging;

namespace Application.Tickets.UpdateEventTicketType;

public sealed record UpdateEventTicketTypeCommand(
    Guid EventId,
    Guid TicketTypeId,
    string? Name,
    string? Description,
    int? PriceCents,
    int? Capacity) : ICommand<TicketTypeResponse>;
