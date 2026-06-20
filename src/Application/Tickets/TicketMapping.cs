using Domain.Tickets;
using Domain.Tickets.Services;

namespace Application.Tickets;

internal static class TicketMapping
{
    public static TicketTypeResponse ToResponse(TicketType ticketType) =>
        new(
            ticketType.Id,
            ticketType.Name,
            ticketType.Description,
            ticketType.PriceCents,
            ticketType.Capacity,
            ticketType.SoldQuantity,
            ticketType.ReservedQuantity,
            TicketTypeService.GetRemainingQuantity(ticketType));

    public static IReadOnlyList<TicketTypeResponse> ToResponses(IEnumerable<TicketType> ticketTypes) =>
        ticketTypes
            .OrderBy(t => t.CreatedAt)
            .Select(ToResponse)
            .ToList();
}
