using Domain.Events;

namespace Application.Tickets.ListMyTickets;

public sealed record MyTicketListItemResponse(
    Guid TicketId,
    Guid OrderId,
    Guid EventId,
    string EventTitle,
    EventStatus EventStatus,
    DateTime StartTime,
    DateTime EndTime,
    string TimeZoneId,
    string TicketTypeName,
    Guid ParticipantId,
    bool ParticipantIsGroup,
    string ParticipantDisplayName,
    bool IsCheckedIn,
    bool RefundPending,
    DateTime? CheckedInAt);

public sealed record MyTicketsResponse(
    IReadOnlyList<MyTicketListItemResponse> PersonalTickets,
    IReadOnlyList<MyTicketListItemResponse> GroupTickets);
