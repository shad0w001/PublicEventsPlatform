namespace Application.Tickets;

public sealed record TicketTypeResponse(
    Guid Id,
    string Name,
    string Description,
    int PriceCents,
    int Capacity,
    int SoldQuantity,
    int ReservedQuantity,
    int RemainingQuantity);
