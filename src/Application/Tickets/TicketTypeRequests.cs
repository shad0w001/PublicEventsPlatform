namespace Application.Tickets;

public sealed record CreateEventTicketTypeRequest(
    string Name,
    string? Description,
    int PriceCents,
    int Capacity);

public sealed record UpdateEventTicketTypeRequest(
    string? Name,
    string? Description,
    int? PriceCents,
    int? Capacity);
