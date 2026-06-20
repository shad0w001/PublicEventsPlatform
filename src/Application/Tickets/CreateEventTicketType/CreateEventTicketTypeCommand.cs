using Application.Abstractions.Messaging;

namespace Application.Tickets.CreateEventTicketType;

public sealed record CreateEventTicketTypeCommand(
    Guid EventId,
    string Name,
    string? Description,
    int PriceCents,
    int Capacity) : ICommand<TicketTypeResponse>;
