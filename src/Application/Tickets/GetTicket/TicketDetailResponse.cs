using Application.Tickets;

namespace Application.Tickets.GetTicket;

public sealed record TicketDetailResponse(
    Guid TicketId,
    Guid OrderId,
    string EventName,
    string TicketTypeName,
    TicketDisplayState State,
    string? ManualCode,
    string? QrPayload);
